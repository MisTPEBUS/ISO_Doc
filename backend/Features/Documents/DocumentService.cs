using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Documents;

public sealed class DocumentService(
    IDocumentStore documentStore,
    ICurrentUser currentUser,
    IValidator<CreateDocumentRequest> createValidator,
    IValidator<UpdateDocumentRequest> updateValidator,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IDocumentService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int MaximumBulkImportSize = 200;

    public async Task<Result<PagedResult<DocumentResponse>>> ListAsync(
        Guid? companyId,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var companyFilter = CompanyAccessRules.ResolveCompanyFilter(
            currentUser.Role, currentUser.CompanyId, companyId);
        if (!companyFilter.IsAllowed)
        {
            return Result<PagedResult<DocumentResponse>>.Forbidden(
                "您沒有檢視這間公司文件的權限。");
        }

        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;
        var totalCount = await documentStore.CountAsync(
            companyFilter.CompanyId, keyword, cancellationToken);
        var documents = await documentStore.ListAsync(
            companyFilter.CompanyId, keyword, (page - 1) * pageSize,
            pageSize, cancellationToken);

        return Result<PagedResult<DocumentResponse>>.Success(new(
            documents.Select(ToResponse).ToArray(), page, pageSize, totalCount));
    }

    public async Task<Result<DocumentResponse>> CreateAsync(
        CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DocumentResponse>.ValidationFailed(ToErrors(validation));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<DocumentResponse>.Forbidden(
                "您沒有為這間公司建立文件的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<DocumentResponse>.Unauthorized("請先登入後再操作。");
        }

        var documentNo = request.DocumentNo!.Trim();
        if (await documentStore.DocumentNoExistsAsync(
            request.CompanyId, documentNo, cancellationToken))
        {
            return DuplicateDocumentNo<DocumentResponse>();
        }

        var now = timeProvider.GetUtcNow();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            DocumentNo = documentNo,
            Name = request.Name!.Trim(),
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        documentStore.Add(document);
        try
        {
            await documentStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateDocumentNoViolation(exception))
        {
            return DuplicateDocumentNo<DocumentResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.CreateDocument,
                AuditResourceTypes.Document,
                document.Id,
                new { new_value = ToAuditValue(document) }),
            cancellationToken);

        return Result<DocumentResponse>.Success(ToResponse(document));
    }

    public async Task<Result<BulkImportDocumentsResponse>> BulkImportAsync(
        BulkImportDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var items = request.Items;
        if (items is null || items.Count == 0)
        {
            return Result<BulkImportDocumentsResponse>.ValidationFailed(
                FieldError("items", "請至少提供一筆文件資料。"));
        }

        if (items.Count > MaximumBulkImportSize)
        {
            return Result<BulkImportDocumentsResponse>.ValidationFailed(
                FieldError("items", $"一次最多可匯入 {MaximumBulkImportSize} 筆文件。"));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<BulkImportDocumentsResponse>.Forbidden(
                "您沒有為這間公司匯入文件的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<BulkImportDocumentsResponse>.Unauthorized("請先登入後再操作。");
        }

        var now = timeProvider.GetUtcNow();
        var reservedDocumentNos = new HashSet<string>(StringComparer.Ordinal);
        var succeeded = new List<BulkImportDocumentSuccess>();
        var failed = new List<BulkImportDocumentFailure>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i + 1;
            var createRequest = new CreateDocumentRequest(
                request.CompanyId, item.DocumentNo, item.Name);
            var validation = await createValidator.ValidateAsync(createRequest, cancellationToken);
            var errors = ToErrors(validation);
            if (!item.PageCount.HasValue)
            {
                AddError(errors, "pageCount", "請輸入頁數。");
            }
            else if (item.PageCount.Value < 0)
            {
                AddError(errors, "pageCount", "頁數不可小於零。");
            }

            if (!DocumentVersionNumber.TryParse(
                    item.Version, out var normalizedVersion, out var versionMajor, out var versionMinor))
            {
                AddError(errors, "version", "版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。");
            }

            if (errors.Count > 0)
            {
                failed.Add(new(index, item, errors));
                continue;
            }

            var documentNo = item.DocumentNo!.Trim();
            if (!reservedDocumentNos.Add(documentNo))
            {
                failed.Add(new(index, item,
                    FieldError("documentNo", "此文件編號與批次中其他筆資料重複。")));
                continue;
            }

            if (await documentStore.DocumentNoExistsAsync(
                request.CompanyId, documentNo, cancellationToken))
            {
                failed.Add(new(index, item, DuplicateDocumentNoError()));
                continue;
            }

            var document = new Document
            {
                Id = Guid.NewGuid(),
                CompanyId = request.CompanyId,
                DocumentNo = documentNo,
                Name = item.Name!.Trim(),
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            var documentVersion = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Version = normalizedVersion,
                VersionMajor = versionMajor,
                VersionMinor = versionMinor,
                Status = "DRAFT",
                PublishDate = null,
                EffectiveDate = item.EffectiveDate,
                ExpiredDate = null,
                PageCount = item.PageCount!.Value,
                CreatedBy = userId,
                CreatedAt = now
            };

            try
            {
                await using var transaction = await documentStore.BeginTransactionAsync(
                    cancellationToken);
                documentStore.Add(document);
                documentStore.Add(documentVersion);
                await documentStore.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateDocumentNoViolation(exception))
            {
                documentStore.Detach(document);
                documentStore.Detach(documentVersion);
                failed.Add(new(index, item, DuplicateDocumentNoError()));
                continue;
            }
            catch (DbUpdateException)
            {
                documentStore.Detach(document);
                documentStore.Detach(documentVersion);
                failed.Add(new(index, item,
                    FieldError("item", "此筆資料寫入失敗，未建立文件及版本。")));
                continue;
            }

            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    document.CompanyId,
                    AuditActions.CreateDocument,
                    AuditResourceTypes.Document,
                    document.Id,
                    new
                    {
                        new_value = ToAuditValue(document),
                        version = new
                        {
                            id = documentVersion.Id,
                            version = documentVersion.Version,
                            status = documentVersion.Status,
                            page_count = documentVersion.PageCount,
                            effective_date = documentVersion.EffectiveDate
                        },
                        bulk_import = true
                    }),
                cancellationToken);
            succeeded.Add(new(index,
                new(
                    document.Id,
                    documentVersion.Id,
                    document.DocumentNo,
                    document.Name,
                    documentVersion.PageCount!.Value,
                    documentVersion.EffectiveDate,
                    documentVersion.Version,
                    documentVersion.Status)));
        }

        return Result<BulkImportDocumentsResponse>.Success(new(
            items.Count, succeeded.Count, failed.Count, succeeded, failed));
    }

    public async Task<Result<DocumentDetailResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await documentStore.FindByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDetailResponse>.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentDetailResponse>.Forbidden(
                "您沒有檢視此文件的權限。");
        }

        var versions = await documentStore.ListVersionsAsync(id, cancellationToken);
        var attachments = await documentStore.ListAttachmentsAsync(id, cancellationToken);
        var versionSummaries = versions.Select(ToVersionSummary).ToArray();
        var currentVersion = versions.FirstOrDefault(version => version.Status == "PUBLISHED")
            ?? versions.FirstOrDefault(version => version.Status == "DRAFT");
        return Result<DocumentDetailResponse>.Success(new(
            document.Id,
            document.CompanyId,
            document.DocumentNo,
            document.Name,
            document.IsActive,
            document.CreatedBy,
            document.CreatedAt,
            document.UpdatedAt,
            currentVersion is null ? null : ToVersionSummary(currentVersion),
            versionSummaries,
            attachments.Select(ToAttachmentSummary).ToArray()));
    }

    public async Task<Result<DocumentResponse>> UpdateAsync(
        Guid id,
        UpdateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DocumentResponse>.ValidationFailed(ToErrors(validation));
        }

        var document = await documentStore.FindByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<DocumentResponse>.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentResponse>.Forbidden(
                "您沒有修改此文件的權限。");
        }

        var oldValue = ToAuditValue(document);
        document.Name = request.Name!.Trim();
        document.UpdatedAt = timeProvider.GetUtcNow();
        await documentStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.UpdateDocument,
                AuditResourceTypes.Document,
                document.Id,
                new
                {
                    old_value = oldValue,
                    new_value = ToAuditValue(document)
                }),
            cancellationToken);
        return Result<DocumentResponse>.Success(ToResponse(document));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documentStore.FindByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result.Forbidden("您沒有停用此文件的權限。");
        }

        var wasActive = document.IsActive;
        document.IsActive = false;
        document.UpdatedAt = timeProvider.GetUtcNow();
        await documentStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.DeleteDocument,
                AuditResourceTypes.Document,
                document.Id,
                new
                {
                    old_value = new { is_active = wasActive },
                    new_value = new { is_active = document.IsActive }
                }),
            cancellationToken);
        return Result.Success();
    }

    private static Result<T> DuplicateDocumentNo<T>() => Result<T>.ValidationFailed(
        DuplicateDocumentNoError());

    private static Dictionary<string, string[]> DuplicateDocumentNoError() =>
        FieldError("documentNo", "這間公司已使用相同的文件編號。");

    private static Dictionary<string, string[]> FieldError(string field, string message) =>
        new(StringComparer.Ordinal) { [field] = [message] };

    private static void AddError(
        Dictionary<string, string[]> errors,
        string field,
        string message)
    {
        errors[field] = errors.TryGetValue(field, out var existing)
            ? [.. existing, message]
            : [message];
    }

    private static bool IsDuplicateDocumentNoViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_documents_company_no"
        };

    private static DocumentResponse ToResponse(Document document) => new(
        document.Id,
        document.CompanyId,
        document.DocumentNo,
        document.Name,
        document.IsActive,
        document.CreatedBy,
        document.CreatedAt,
        document.UpdatedAt);

    private static DocumentVersionSummary ToVersionSummary(DocumentVersion version) => new(
        version.Id,
        version.Version,
        version.Status,
        version.PublishDate,
        version.EffectiveDate,
        version.ExpiredDate,
        version.PageCount,
        version.FileKey is not null);

    private static DocumentAttachmentSummary ToAttachmentSummary(
        DocumentAttachmentRecord record) => new(
        record.Attachment.Id,
        record.Attachment.AttachmentNo,
        record.Attachment.Name,
        record.Attachment.IsActive,
        record.CurrentVersion is null
            ? null
            : new AttachmentVersionSummary(
                record.CurrentVersion.Id,
                record.CurrentVersion.Version,
                record.CurrentVersion.Status,
                record.CurrentVersion.PublishDate,
                record.CurrentVersion.EffectiveDate,
                record.CurrentVersion.ExpiredDate,
                record.CurrentVersion.FileKey is not null));

    private static object ToAuditValue(Document document) => new
    {
        document_no = document.DocumentNo,
        name = document.Name,
        is_active = document.IsActive
    };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
