using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Depts.Dtos;
using IsoDocument.Api.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Depts;

public sealed class DeptService(
    IDeptStore deptStore,
    ICurrentUser currentUser,
    IValidator<CreateDeptRequest> createValidator,
    IValidator<UpdateDeptRequest> updateValidator,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IDeptService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<Result<PagedResult<DeptResponse>>> ListAsync(
        Guid? companyId,
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
            return Result<PagedResult<DeptResponse>>.Forbidden(
                "You do not have permission to access departments for this company.");
        }

        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;
        var totalCount = await deptStore.CountAsync(companyFilter.CompanyId, cancellationToken);
        var depts = await deptStore.ListAsync(
            companyFilter.CompanyId,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken);

        return Result<PagedResult<DeptResponse>>.Success(new PagedResult<DeptResponse>(
            depts.Select(ToResponse).ToArray(),
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<DeptResponse>> CreateAsync(
        CreateDeptRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DeptResponse>.ValidationFailed(ToErrors(validation));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<DeptResponse>.Forbidden(
                "You do not have permission to create departments for this company.");
        }

        var name = request.Name!.Trim();
        if (await deptStore.NameExistsAsync(
            request.CompanyId,
            name,
            excludingDeptId: null,
            cancellationToken))
        {
            return DuplicateName<DeptResponse>();
        }

        var now = timeProvider.GetUtcNow();
        var dept = new Dept
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            Name = name,
            Seq = request.Seq,
            CreatedAt = now,
            UpdatedAt = now
        };
        deptStore.Add(dept);
        try
        {
            await deptStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return DuplicateName<DeptResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                dept.CompanyId,
                AuditActions.CreateDept,
                AuditResourceTypes.Dept,
                dept.Id,
                new
                {
                    new_value = new { name = dept.Name, seq = dept.Seq }
                }),
            cancellationToken);

        return Result<DeptResponse>.Success(ToResponse(dept));
    }

    public async Task<Result<DeptResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dept = await deptStore.FindByIdAsync(id, cancellationToken);
        if (dept is null)
        {
            return Result<DeptResponse>.NotFound("The department was not found.");
        }

        return currentUser.CanAccessCompany(dept.CompanyId)
            ? Result<DeptResponse>.Success(ToResponse(dept))
            : Result<DeptResponse>.Forbidden(
                "You do not have permission to access this department.");
    }

    public async Task<Result<DeptResponse>> UpdateAsync(
        Guid id,
        UpdateDeptRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DeptResponse>.ValidationFailed(ToErrors(validation));
        }

        var dept = await deptStore.FindByIdAsync(id, cancellationToken);
        if (dept is null)
        {
            return Result<DeptResponse>.NotFound("The department was not found.");
        }

        if (!currentUser.CanAccessCompany(dept.CompanyId))
        {
            return Result<DeptResponse>.Forbidden(
                "You do not have permission to update this department.");
        }

        var name = request.Name!.Trim();
        if (await deptStore.NameExistsAsync(
            dept.CompanyId,
            name,
            dept.Id,
            cancellationToken))
        {
            return DuplicateName<DeptResponse>();
        }

        var oldValue = new { name = dept.Name, seq = dept.Seq };
        dept.Name = name;
        dept.Seq = request.Seq;
        dept.UpdatedAt = timeProvider.GetUtcNow();
        try
        {
            await deptStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return DuplicateName<DeptResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                dept.CompanyId,
                AuditActions.UpdateDept,
                AuditResourceTypes.Dept,
                dept.Id,
                new
                {
                    old_value = oldValue,
                    new_value = new { name = dept.Name, seq = dept.Seq }
                }),
            cancellationToken);

        return Result<DeptResponse>.Success(ToResponse(dept));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var dept = await deptStore.FindByIdAsync(id, cancellationToken);
        if (dept is null)
        {
            return Result.NotFound("The department was not found.");
        }

        if (!currentUser.CanAccessCompany(dept.CompanyId))
        {
            return Result.Forbidden("You do not have permission to delete this department.");
        }

        if (await deptStore.HasActiveUsersAsync(id, cancellationToken))
        {
            return Result.Conflict(
                "The department cannot be deleted because it still has active users.");
        }

        var oldValue = new { name = dept.Name, seq = dept.Seq };
        deptStore.Remove(dept);
        await deptStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                dept.CompanyId,
                AuditActions.DeleteDept,
                AuditResourceTypes.Dept,
                dept.Id,
                new { old_value = oldValue }),
            cancellationToken);
        return Result.Success();
    }

    private static Result<T> DuplicateName<T>() => Result<T>.Conflict(
        "A department with the same name already exists in this company.");

    private static bool IsDuplicateNameViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_depts_company_name"
        };

    private static DeptResponse ToResponse(Dept dept) => new(
        dept.Id,
        dept.CompanyId,
        dept.Name,
        dept.Seq,
        dept.CreatedAt,
        dept.UpdatedAt);

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
