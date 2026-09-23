using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.IsoCategories.Dtos;
using IsoDocument.Api.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.IsoCategories;

public sealed class IsoCategoryService(
    IIsoCategoryStore isoCategoryStore,
    ICurrentUser currentUser,
    IValidator<CreateIsoCategoryRequest> createValidator,
    IValidator<UpdateIsoCategoryRequest> updateValidator,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IIsoCategoryService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<Result<PagedResult<IsoCategoryResponse>>> ListAsync(
        Guid? companyId,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var companyFilter = CompanyAccessRules.ResolveCompanyFilter(
            currentUser.Role,
            currentUser.CompanyId,
            companyId);
        if (!companyFilter.IsAllowed)
        {
            return Result<PagedResult<IsoCategoryResponse>>.Forbidden(
                "您沒有檢視這間公司品質系統的權限。");
        }

        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;
        var totalCount = await isoCategoryStore.CountAsync(
            companyFilter.CompanyId, includeInactive, cancellationToken);
        var categories = await isoCategoryStore.ListAsync(
            companyFilter.CompanyId,
            includeInactive,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken);

        return Result<PagedResult<IsoCategoryResponse>>.Success(new PagedResult<IsoCategoryResponse>(
            categories.Select(ToResponse).ToArray(),
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<IsoCategoryResponse>> CreateAsync(
        CreateIsoCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<IsoCategoryResponse>.ValidationFailed(ToErrors(validation));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<IsoCategoryResponse>.Forbidden(
                "您沒有為這間公司建立品質系統的權限。");
        }

        var name = request.Name!.Trim();
        if (await isoCategoryStore.NameExistsAsync(
            request.CompanyId,
            name,
            excludingIsoCategoryId: null,
            cancellationToken))
        {
            return DuplicateName<IsoCategoryResponse>();
        }

        var now = timeProvider.GetUtcNow();
        var category = new IsoCategory
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            Name = name,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        isoCategoryStore.Add(category);
        try
        {
            await isoCategoryStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return DuplicateName<IsoCategoryResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                category.CompanyId,
                AuditActions.CreateIsoCategory,
                AuditResourceTypes.IsoCategory,
                category.Id,
                new { new_value = ToAuditValue(category) }),
            cancellationToken);

        return Result<IsoCategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result<IsoCategoryResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await isoCategoryStore.FindByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return Result<IsoCategoryResponse>.NotFound("找不到指定的品質系統。");
        }

        return currentUser.CanAccessCompany(category.CompanyId)
            ? Result<IsoCategoryResponse>.Success(ToResponse(category))
            : Result<IsoCategoryResponse>.Forbidden(
                "您沒有檢視此品質系統的權限。");
    }

    public async Task<Result<IsoCategoryResponse>> UpdateAsync(
        Guid id,
        UpdateIsoCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<IsoCategoryResponse>.ValidationFailed(ToErrors(validation));
        }

        var category = await isoCategoryStore.FindByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return Result<IsoCategoryResponse>.NotFound("找不到指定的品質系統。");
        }

        if (!currentUser.CanAccessCompany(category.CompanyId))
        {
            return Result<IsoCategoryResponse>.Forbidden(
                "您沒有修改此品質系統的權限。");
        }

        var name = request.Name!.Trim();
        if (await isoCategoryStore.NameExistsAsync(
            category.CompanyId,
            name,
            category.Id,
            cancellationToken))
        {
            return DuplicateName<IsoCategoryResponse>();
        }

        var oldValue = ToAuditValue(category);
        category.Name = name;
        category.IsActive = request.IsActive;
        category.UpdatedAt = timeProvider.GetUtcNow();
        try
        {
            await isoCategoryStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return DuplicateName<IsoCategoryResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                category.CompanyId,
                AuditActions.UpdateIsoCategory,
                AuditResourceTypes.IsoCategory,
                category.Id,
                new
                {
                    old_value = oldValue,
                    new_value = ToAuditValue(category)
                }),
            cancellationToken);

        return Result<IsoCategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await isoCategoryStore.FindByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.NotFound("找不到指定的品質系統。");
        }

        if (!currentUser.CanAccessCompany(category.CompanyId))
        {
            return Result.Forbidden("您沒有刪除此品質系統的權限。");
        }

        var wasActive = category.IsActive;
        category.IsActive = false;
        category.UpdatedAt = timeProvider.GetUtcNow();
        await isoCategoryStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                category.CompanyId,
                AuditActions.DeleteIsoCategory,
                AuditResourceTypes.IsoCategory,
                category.Id,
                new
                {
                    old_value = new { is_active = wasActive },
                    new_value = new { is_active = category.IsActive }
                }),
            cancellationToken);
        return Result.Success();
    }

    private static Result<T> DuplicateName<T>() => Result<T>.Conflict(
        "這間公司已有相同名稱的品質系統。");

    private static bool IsDuplicateNameViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_iso_categories_company_name"
        };

    private static IsoCategoryResponse ToResponse(IsoCategory category) => new(
        category.Id,
        category.CompanyId,
        category.Name,
        category.IsActive,
        category.CreatedAt,
        category.UpdatedAt);

    private static object ToAuditValue(IsoCategory category) => new
    {
        name = category.Name,
        is_active = category.IsActive
    };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
