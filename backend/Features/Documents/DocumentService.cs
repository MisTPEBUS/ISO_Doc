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
        return Result<DocumentDetailResponse>.Success(new(
            document.Id,
            document.CompanyId,
            document.DocumentNo,
            document.Name,
            document.IsActive,
            document.CreatedBy,
            document.CreatedAt,
            document.UpdatedAt,
            versions.Select(version => new DocumentVersionSummary(
                version.Version,
                version.Status,
                version.EffectiveDate,
                version.ExpiredDate)).ToArray()));
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
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["documentNo"] = ["這間公司已使用相同的文件編號。"]
        });

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
