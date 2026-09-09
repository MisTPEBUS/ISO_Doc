using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
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
                "You do not have permission to access documents for this company.");
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
                "You do not have permission to create documents for this company.");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<DocumentResponse>.Unauthorized("Authentication is required.");
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

        return Result<DocumentResponse>.Success(ToResponse(document));
    }

    public async Task<Result<DocumentDetailResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await documentStore.FindByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDetailResponse>.NotFound("The document was not found.");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentDetailResponse>.Forbidden(
                "You do not have permission to access this document.");
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
            return Result<DocumentResponse>.NotFound("The document was not found.");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentResponse>.Forbidden(
                "You do not have permission to update this document.");
        }

        document.Name = request.Name!.Trim();
        document.UpdatedAt = timeProvider.GetUtcNow();
        await documentStore.SaveChangesAsync(cancellationToken);
        return Result<DocumentResponse>.Success(ToResponse(document));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documentStore.FindByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result.NotFound("The document was not found.");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result.Forbidden("You do not have permission to deactivate this document.");
        }

        document.IsActive = false;
        document.UpdatedAt = timeProvider.GetUtcNow();
        await documentStore.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Result<T> DuplicateDocumentNo<T>() => Result<T>.ValidationFailed(
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["documentNo"] = ["The document number is already in use for this company."]
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

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
