using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Documents;

public sealed class DocumentService(
    IDocumentStore documentStore,
    ICurrentUser currentUser,
    IValidator<CreateDocumentRequest> createValidator,
    IValidator<UpdateDocumentRequest> updateValidator,
    IValidator<CreateDocumentVersionRequest> versionValidator,
    IDocumentStorage documentStorage,
    StorageObjectKeyService storageKeyBuilder,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider,
    ILogger<DocumentService> logger) : IDocumentService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int MaximumBulkImportSize = 200;

    public async Task<Result<PagedResult<DocumentResponse>>> ListAsync(
        Guid? companyId,
        string? keyword,
        string? sortBy,
        string? sortDirection,
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

        if (!ListSort.TryParse(
                sortBy, sortDirection, DocumentSortFields.All, out var sort, out var sortErrors))
        {
            return Result<PagedResult<DocumentResponse>>.ValidationFailed(sortErrors);
        }

        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;
        var totalCount = await documentStore.CountAsync(
            companyFilter.CompanyId, keyword, cancellationToken);
        var documents = await documentStore.ListAsync(
            companyFilter.CompanyId, keyword, sort, (page - 1) * pageSize,
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
        var (dept, referenceErrors) = await CheckNewDocumentReferencesAsync(
            request, documentNo, cancellationToken);
        if (referenceErrors is not null)
        {
            return Result<DocumentResponse>.ValidationFailed(referenceErrors);
        }

        var now = timeProvider.GetUtcNow();
        var document = NewDocument(request, documentNo, dept, userId, now);

        // 文件建立與「預設全開」部門權限必須在同一個 transaction 內完成，
        // 避免文件建立成功但權限沒建立（或相反）的不一致狀態。
        await using var transaction = await documentStore.BeginTransactionAsync(cancellationToken);
        documentStore.Add(document);

        var deptIds = await documentStore.ListCompanyDeptIdsAsync(
            request.CompanyId, cancellationToken);
        var permissions = DefaultDocumentPermissions.Create(document.Id, deptIds, userId, now);
        if (permissions.Length > 0)
        {
            documentStore.AddRange(permissions);
        }

        try
        {
            await documentStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateDocumentNoViolation(exception))
        {
            return DuplicateDocumentNo<DocumentResponse>();
        }

        await WriteCreateDocumentAuditsAsync(document, deptIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<DocumentResponse>.Success(ToResponse(document));
    }

    public async Task<Result<CreateDocumentWithVersionResponse>> CreateWithVersionAsync(
        CreateDocumentWithVersionRequest request,
        CancellationToken cancellationToken)
    {
        var documentRequest = new CreateDocumentRequest(
            request.CompanyId, request.DocumentNo, request.Name,
            request.IsoCategoryId, request.DeptId);
        var versionRequest = new CreateDocumentVersionRequest
        {
            Version = request.Version,
            // 生效日期空白時以 UTC 今日立即生效（SPEC 第 5 節）。
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(
                timeProvider.GetUtcNow().UtcDateTime),
            PageCount = request.PageCount,
            Memo = request.Memo,
            File = request.File
        };

        var errors = ToErrors(await createValidator.ValidateAsync(
            documentRequest, cancellationToken));
        foreach (var (field, messages) in ToErrors(await versionValidator.ValidateAsync(
                     versionRequest, cancellationToken)))
        {
            errors[field] = messages;
        }

        if (errors.Count == 0
            && !await DocumentFileRules.HasPdfMagicBytesAsync(request.File!, cancellationToken))
        {
            AddError(errors, "file", "文件內容不是有效的 PDF 檔案。");
        }

        if (errors.Count > 0)
        {
            return Result<CreateDocumentWithVersionResponse>.ValidationFailed(errors);
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<CreateDocumentWithVersionResponse>.Forbidden(
                "您沒有為這間公司建立文件的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<CreateDocumentWithVersionResponse>.Unauthorized("請先登入後再操作。");
        }

        var documentNo = request.DocumentNo!.Trim();
        var (dept, referenceErrors) = await CheckNewDocumentReferencesAsync(
            documentRequest, documentNo, cancellationToken);
        if (referenceErrors is not null)
        {
            return Result<CreateDocumentWithVersionResponse>.ValidationFailed(referenceErrors);
        }

        var companyCode = await documentStore.FindCompanyCodeAsync(
            request.CompanyId, cancellationToken);
        if (companyCode is null)
        {
            return Result<CreateDocumentWithVersionResponse>.NotFound("找不到文件所屬的公司。");
        }

        DocumentVersionNumber.TryParse(
            versionRequest.Version, out var versionText, out var major, out var minor);
        var now = timeProvider.GetUtcNow();
        var document = NewDocument(documentRequest, documentNo, dept, userId, now);
        var deptIds = await documentStore.ListCompanyDeptIdsAsync(
            request.CompanyId, cancellationToken);
        var permissions = DefaultDocumentPermissions.Create(document.Id, deptIds, userId, now);
        var file = request.File!;
        var objectKey = await storageKeyBuilder.BuildMainKeyAsync(
            document, companyCode, versionText, Guid.NewGuid(), file.FileName, cancellationToken);

        // 儲存體不在 DB transaction 內：先寫檔（失敗時 DB 完全未動），
        // 之後 transaction 任一步失敗就 rollback 並把已寫入的檔案搬到 trash 補償。
        StorageWriteResult writeResult;
        await using (var fileStream = file.OpenReadStream())
        {
            writeResult = await documentStorage.WriteAsync(
                objectKey, fileStream, cancellationToken);
        }

        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Version = versionText,
            VersionMajor = major,
            VersionMinor = minor,
            Status = "PUBLISHED",
            PublishDate = DateOnly.FromDateTime(now.UtcDateTime),
            EffectiveDate = versionRequest.EffectiveDate,
            PageCount = request.PageCount,
            Memo = string.IsNullOrWhiteSpace(request.Memo) ? null : request.Memo.Trim(),
            FileKey = objectKey,
            OriginalFileName = file.FileName,
            ContentType = "application/pdf",
            FileSize = writeResult.FileSize,
            Checksum = writeResult.Checksum,
            CreatedBy = userId,
            CreatedAt = now
        };

        // 文件、預設部門權限、第一個版本與 audit 全部在同一個 transaction：一起成功或一起 rollback。
        try
        {
            await using var transaction = await documentStore.BeginTransactionAsync(
                cancellationToken);
            documentStore.Add(document);
            if (permissions.Length > 0)
            {
                documentStore.AddRange(permissions);
            }
            documentStore.Add(version);
            await documentStore.SaveChangesAsync(cancellationToken);

            await WriteCreateDocumentAuditsAsync(document, deptIds, cancellationToken);
            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    document.CompanyId,
                    AuditActions.PublishDocumentVersion,
                    AuditResourceTypes.DocumentVersion,
                    version.Id,
                    new
                    {
                        document_id = document.Id,
                        version = version.Version,
                        effective_date = version.EffectiveDate,
                        previous_published_version_ids = Array.Empty<Guid>()
                    }),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            documentStore.Detach(document);
            documentStore.DetachRange(permissions);
            documentStore.Detach(version);
            await StorageCleanup.TryMoveToTrashAsync(documentStorage, objectKey, logger);

            if (exception is DbUpdateException dbUpdateException
                && IsDuplicateDocumentNoViolation(dbUpdateException))
            {
                return DuplicateDocumentNo<CreateDocumentWithVersionResponse>();
            }

            throw;
        }

        return Result<CreateDocumentWithVersionResponse>.Success(new(
            ToResponse(document),
            new DocumentVersionResponse(version.Id, version.Version, version.Status)));
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
        // 同一批次共用一個 companyId（見 BulkImportDocumentsRequest），部門清單只需查一次。
        var companyDeptIds = await documentStore.ListCompanyDeptIdsAsync(
            request.CompanyId, cancellationToken);
        var reservedDocumentNos = new HashSet<string>(StringComparer.Ordinal);
        var succeeded = new List<BulkImportDocumentSuccess>();
        var failed = new List<BulkImportDocumentFailure>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i + 1;
            var createRequest = new CreateDocumentRequest(
                request.CompanyId, item.DocumentNo, item.Name,
                IsoCategoryId: item.IsoCategoryId, DeptId: item.DeptId);
            var validation = await createValidator.ValidateAsync(createRequest, cancellationToken);
            var errors = ToErrors(validation);
            if (item.IsoCategoryId is { } isoCategoryId
                && !await documentStore.IsoCategoryBelongsToCompanyAsync(
                    request.CompanyId, isoCategoryId, cancellationToken))
            {
                AddError(errors, "isoCategoryId", "指定的品質系統不存在，或不屬於此公司。");
            }
            if (item.DeptId is { } issuingDeptId && !companyDeptIds.Contains(issuingDeptId))
            {
                AddError(errors, "deptId", "指定的發行單位不存在，或不屬於此公司。");
            }
            if (item.PageCount is < 0)
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
                IsoCategoryId = item.IsoCategoryId,
                DeptId = item.DeptId,
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
                PageCount = item.PageCount,
                CreatedBy = userId,
                CreatedAt = now
            };

            var permissions = DefaultDocumentPermissions.Create(
                document.Id, companyDeptIds, userId, now);

            try
            {
                await using var transaction = await documentStore.BeginTransactionAsync(
                    cancellationToken);
                documentStore.Add(document);
                documentStore.Add(documentVersion);
                if (permissions.Length > 0)
                {
                    documentStore.AddRange(permissions);
                }
                await documentStore.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateDocumentNoViolation(exception))
            {
                documentStore.Detach(document);
                documentStore.Detach(documentVersion);
                documentStore.DetachRange(permissions);
                failed.Add(new(index, item, DuplicateDocumentNoError()));
                continue;
            }
            catch (DbUpdateException)
            {
                documentStore.Detach(document);
                documentStore.Detach(documentVersion);
                documentStore.DetachRange(permissions);
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

            if (permissions.Length > 0)
            {
                await auditLogService.WriteAsync(
                    new AuditLogWriteRequest(
                        document.CompanyId,
                        AuditActions.UpdateDocumentDeptPermissions,
                        AuditResourceTypes.Document,
                        document.Id,
                        new
                        {
                            old_value = Array.Empty<Guid>(),
                            new_value = companyDeptIds
                        }),
                    cancellationToken);
            }

            succeeded.Add(new(index,
                new(
                    document.Id,
                    documentVersion.Id,
                    document.DocumentNo,
                    document.Name,
                    document.IsoCategoryId,
                    documentVersion.PageCount,
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
            document.IsoCategoryId,
            document.DeptId,
            document.Dept?.Name,
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

        if (request.IsoCategoryId is { } updateIsoCategoryId
            && !await documentStore.IsoCategoryBelongsToCompanyAsync(
                document.CompanyId, updateIsoCategoryId, cancellationToken))
        {
            return Result<DocumentResponse>.ValidationFailed(
                FieldError("isoCategoryId", "指定的品質系統不存在，或不屬於此公司。"));
        }

        Dept? updateDept = null;
        if (request.DeptId is { } updateDeptId)
        {
            updateDept = await documentStore.FindCompanyDeptAsync(
                document.CompanyId, updateDeptId, cancellationToken);
            if (updateDept is null)
            {
                return Result<DocumentResponse>.ValidationFailed(
                    FieldError("deptId", "指定的發行單位不存在，或不屬於此公司。"));
            }
        }

        var oldValue = ToAuditValue(document);
        document.Name = request.Name!.Trim();
        document.IsoCategoryId = request.IsoCategoryId;
        document.DeptId = request.DeptId;
        document.Dept = updateDept;
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

    /// <summary>
    /// 新增文件前的關聯檢查：同公司文件編號不可重複，品質系統與發行單位須屬於該公司。
    /// 通過時回傳發行單位（讓回應可直接帶出 deptName），失敗時回傳欄位錯誤。
    /// </summary>
    private async Task<(Dept? Dept, Dictionary<string, string[]>? Errors)>
        CheckNewDocumentReferencesAsync(
            CreateDocumentRequest request,
            string documentNo,
            CancellationToken cancellationToken)
    {
        if (await documentStore.DocumentNoExistsAsync(
            request.CompanyId, documentNo, cancellationToken))
        {
            return (null, DuplicateDocumentNoError());
        }

        if (request.IsoCategoryId is { } isoCategoryId
            && !await documentStore.IsoCategoryBelongsToCompanyAsync(
                request.CompanyId, isoCategoryId, cancellationToken))
        {
            return (null, FieldError("isoCategoryId", "指定的品質系統不存在，或不屬於此公司。"));
        }

        if (request.DeptId is not { } deptId)
        {
            return (null, null);
        }

        var dept = await documentStore.FindCompanyDeptAsync(
            request.CompanyId, deptId, cancellationToken);
        return dept is null
            ? (null, FieldError("deptId", "指定的發行單位不存在，或不屬於此公司。"))
            : (dept, null);
    }

    private static Document NewDocument(
        CreateDocumentRequest request,
        string documentNo,
        Dept? dept,
        Guid userId,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = request.CompanyId,
        DocumentNo = documentNo,
        Name = request.Name!.Trim(),
        IsActive = true,
        IsoCategoryId = request.IsoCategoryId,
        DeptId = request.DeptId,
        Dept = dept,
        CreatedBy = userId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private async Task WriteCreateDocumentAuditsAsync(
        Document document,
        IReadOnlyList<Guid> deptIds,
        CancellationToken cancellationToken)
    {
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.CreateDocument,
                AuditResourceTypes.Document,
                document.Id,
                new { new_value = ToAuditValue(document) }),
            cancellationToken);

        if (deptIds.Count > 0)
        {
            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    document.CompanyId,
                    AuditActions.UpdateDocumentDeptPermissions,
                    AuditResourceTypes.Document,
                    document.Id,
                    new
                    {
                        old_value = Array.Empty<Guid>(),
                        new_value = deptIds
                    }),
                cancellationToken);
        }
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
        document.UpdatedAt,
        document.IsoCategoryId,
        document.DeptId,
        document.Dept?.Name);

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
        is_active = document.IsActive,
        iso_category_id = document.IsoCategoryId,
        dept_id = document.DeptId
    };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
