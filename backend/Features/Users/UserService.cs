using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Users.Dtos;
using IsoDocument.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Users;

public sealed class UserService(
    IUserStore userStore,
    ICurrentUser currentUser,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IPasswordHasher<User> passwordHasher,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IUserService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int MaximumBatchSize = 200;
    private const string TemporaryPasswordAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public async Task<Result<PagedResult<UserResponse>>> ListAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var companyFilter = CompanyAccessRules.ResolveCompanyFilter(
            currentUser.Role, currentUser.CompanyId, companyId);
        if (!companyFilter.IsAllowed)
        {
            return Result<PagedResult<UserResponse>>.Forbidden(
                "您沒有檢視這間公司使用者資料的權限。");
        }

        if (deptId.HasValue && companyFilter.CompanyId.HasValue
            && !await userStore.DeptBelongsToCompanyAsync(
                deptId.Value, companyFilter.CompanyId.Value, cancellationToken))
        {
            return Result<PagedResult<UserResponse>>.ValidationFailed(
                FieldError("deptId", "指定的部門不屬於這間公司。"));
        }

        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;
        var totalCount = await userStore.CountAsync(
            companyFilter.CompanyId, deptId, keyword, includeInactive, cancellationToken);
        var users = await userStore.ListAsync(
            companyFilter.CompanyId, deptId, keyword, includeInactive,
            (page - 1) * pageSize, pageSize, cancellationToken);

        return Result<PagedResult<UserResponse>>.Success(new(
            users.Select(ToResponse).ToArray(), page, pageSize, totalCount));
    }

    public async Task<Result<UserResponse>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UserResponse>.ValidationFailed(ToErrors(validation));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<UserResponse>.Forbidden(
                "您沒有為這間公司建立使用者的權限。");
        }

        var requestedRole = Enum.Parse<UserRole>(request.Role!, ignoreCase: false);
        if (!UserManagementRules.CanCreateRole(currentUser.Role, requestedRole))
        {
            return Result<UserResponse>.Forbidden(
                "公司管理員無法建立系統管理員帳號。");
        }

        if (!await userStore.DeptBelongsToCompanyAsync(
            request.DeptId, request.CompanyId, cancellationToken))
        {
            return Result<UserResponse>.ValidationFailed(
                FieldError("deptId", "指定的部門不屬於這間公司。"));
        }

        var empno = request.Empno!.Trim();
        if (await userStore.EmpnoExistsAsync(empno, cancellationToken))
        {
            return DuplicateEmpno<UserResponse>();
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            DeptId = request.DeptId,
            Empno = empno,
            Name = request.Name!.Trim(),
            Email = NormalizeOptional(request.Email),
            Role = requestedRole.ToString(),
            IsActive = true,
            MustChangePassword = false,
            NotifyEmailEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (request.Password is not null)
        {
            user.PasswordDigest = passwordHasher.HashPassword(user, request.Password);
        }

        userStore.Add(user);
        try
        {
            await userStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmpnoViolation(exception))
        {
            return DuplicateEmpno<UserResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                user.CompanyId,
                AuditActions.CreateUser,
                AuditResourceTypes.User,
                user.Id,
                new { new_value = ToAuditValue(user) }),
            cancellationToken);

        return Result<UserResponse>.Success(ToResponse(user));
    }

    public async Task<Result<BatchCreateUsersResponse>> BatchCreateAsync(
        BatchCreateUsersRequest request,
        CancellationToken cancellationToken)
    {
        var items = request.Users;
        if (items is null || items.Count == 0)
        {
            return Result<BatchCreateUsersResponse>.ValidationFailed(
                FieldError("users", "請至少提供一筆使用者資料。"));
        }

        if (items.Count > MaximumBatchSize)
        {
            return Result<BatchCreateUsersResponse>.ValidationFailed(
                FieldError("users", $"一次最多可新增 {MaximumBatchSize} 筆使用者。"));
        }

        var now = timeProvider.GetUtcNow();
        var reservedEmpnos = new HashSet<string>(StringComparer.Ordinal);
        var succeeded = new List<BatchCreateUserSuccess>();
        var failed = new List<BatchCreateUserFailure>();

        for (var i = 0; i < items.Count; i++)
        {
            var index = i + 1;
            var item = items[i];
            var (user, errors) = await BuildBatchUserAsync(
                item, reservedEmpnos, now, cancellationToken);
            if (user is null)
            {
                failed.Add(new BatchCreateUserFailure(index, item, errors!));
                continue;
            }

            userStore.Add(user);
            try
            {
                await userStore.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateEmpnoViolation(exception))
            {
                userStore.Detach(user);
                failed.Add(new BatchCreateUserFailure(index, item, DuplicateEmpnoError()));
                continue;
            }

            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    user.CompanyId,
                    AuditActions.CreateUser,
                    AuditResourceTypes.User,
                    user.Id,
                    new { new_value = ToAuditValue(user) }),
                cancellationToken);

            succeeded.Add(new BatchCreateUserSuccess(index, ToResponse(user)));
        }

        if (succeeded.Count > 0)
        {
            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    null,
                    AuditActions.BatchCreateUsers,
                    AuditResourceTypes.User,
                    null,
                    new { total = items.Count, success_count = succeeded.Count, failure_count = failed.Count }),
                cancellationToken);
        }

        return Result<BatchCreateUsersResponse>.Success(new BatchCreateUsersResponse(
            items.Count, succeeded.Count, failed.Count, succeeded, failed));
    }

    public async Task<Result<UserResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userStore.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result<UserResponse>.NotFound("找不到指定的使用者。");
        }

        return currentUser.CanAccessCompany(user.CompanyId)
            ? Result<UserResponse>.Success(ToResponse(user))
            : Result<UserResponse>.Forbidden("您沒有檢視此使用者的權限。");
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UserResponse>.ValidationFailed(ToErrors(validation));
        }

        var user = await userStore.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result<UserResponse>.NotFound("找不到指定的使用者。");
        }

        if (!currentUser.CanAccessCompany(user.CompanyId))
        {
            return Result<UserResponse>.Forbidden("您沒有修改此使用者的權限。");
        }

        var existingRole = Enum.Parse<UserRole>(user.Role, ignoreCase: false);
        var requestedRole = Enum.Parse<UserRole>(request.Role!, ignoreCase: false);
        if (!UserManagementRules.CanUpdateRole(currentUser.Role, existingRole, requestedRole))
        {
            return Result<UserResponse>.Forbidden(
                "公司管理員無法建立或修改系統管理員帳號。");
        }

        if (!await userStore.DeptBelongsToCompanyAsync(
            request.DeptId, user.CompanyId, cancellationToken))
        {
            return Result<UserResponse>.ValidationFailed(
                FieldError("deptId", "指定的部門不屬於這間公司。"));
        }

        var oldValue = ToAuditValue(user);
        user.Name = request.Name!.Trim();
        user.Email = NormalizeOptional(request.Email);
        user.DeptId = request.DeptId;
        user.Role = requestedRole.ToString();
        user.IsActive = request.IsActive;
        user.NotifyEmailEnabled = request.NotifyEmailEnabled;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await userStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                user.CompanyId,
                AuditActions.UpdateUser,
                AuditResourceTypes.User,
                user.Id,
                new
                {
                    old_value = oldValue,
                    new_value = ToAuditValue(user)
                }),
            cancellationToken);
        return Result<UserResponse>.Success(ToResponse(user));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userStore.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("找不到指定的使用者。");
        }

        if (!currentUser.CanAccessCompany(user.CompanyId))
        {
            return Result.Forbidden("您沒有停用此使用者的權限。");
        }

        var wasActive = user.IsActive;
        user.IsActive = false;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await userStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                user.CompanyId,
                AuditActions.DeleteUser,
                AuditResourceTypes.User,
                user.Id,
                new
                {
                    old_value = new { is_active = wasActive },
                    new_value = new { is_active = user.IsActive }
                }),
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ResetPasswordResponse>> ResetPasswordAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userStore.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result<ResetPasswordResponse>.NotFound("找不到指定的使用者。");
        }

        if (!currentUser.CanAccessCompany(user.CompanyId))
        {
            return Result<ResetPasswordResponse>.Forbidden(
                "您沒有重設此使用者密碼的權限。");
        }

        var temporaryPassword = RandomNumberGenerator.GetString(TemporaryPasswordAlphabet, 12);
        user.PasswordDigest = passwordHasher.HashPassword(user, temporaryPassword);
        user.MustChangePassword = true;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await userStore.SaveChangesAsync(cancellationToken);

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                user.CompanyId,
                AuditActions.ResetUserPassword,
                AuditResourceTypes.User,
                user.Id,
                new { new_value = new { must_change_password = true } }),
            cancellationToken);

        return Result<ResetPasswordResponse>.Success(new(temporaryPassword));
    }

    private async Task<(User? User, Dictionary<string, string[]>? Errors)> BuildBatchUserAsync(
        CreateUserRequest request,
        HashSet<string> reservedEmpnos,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return (null, ToErrors(validation));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return (null, GeneralError("您沒有為這間公司建立使用者的權限。"));
        }

        var requestedRole = Enum.Parse<UserRole>(request.Role!, ignoreCase: false);
        if (!UserManagementRules.CanCreateRole(currentUser.Role, requestedRole))
        {
            return (null, GeneralError("公司管理員無法建立系統管理員帳號。"));
        }

        if (!await userStore.DeptBelongsToCompanyAsync(
            request.DeptId, request.CompanyId, cancellationToken))
        {
            return (null, FieldError("deptId", "指定的部門不屬於這間公司。"));
        }

        var empno = request.Empno!.Trim();
        if (!reservedEmpnos.Add(empno))
        {
            return (null, FieldError("empno", "此帳號與批次中其他筆資料重複。"));
        }

        if (await userStore.EmpnoExistsAsync(empno, cancellationToken))
        {
            return (null, DuplicateEmpnoError());
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            DeptId = request.DeptId,
            Empno = empno,
            Name = request.Name!.Trim(),
            Email = NormalizeOptional(request.Email),
            Role = requestedRole.ToString(),
            IsActive = true,
            MustChangePassword = false,
            NotifyEmailEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (request.Password is not null)
        {
            user.PasswordDigest = passwordHasher.HashPassword(user, request.Password);
        }

        return (user, null);
    }

    private static Result<T> DuplicateEmpno<T>() => Result<T>.ValidationFailed(
        FieldError("empno", "此帳號已被使用。"));

    private static Dictionary<string, string[]> DuplicateEmpnoError() =>
        FieldError("empno", "此帳號已被使用。");

    private static Dictionary<string, string[]> GeneralError(string message) =>
        new(StringComparer.Ordinal) { ["_error"] = [message] };

    private static bool IsDuplicateEmpnoViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_users_empno"
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserResponse ToResponse(User user) => new(
        user.Id, user.CompanyId, user.DeptId, user.Empno, user.Name, user.Email,
        user.Role, user.IsActive, user.MustChangePassword, user.NotifyEmailEnabled,
        user.LastLoginAt, user.CreatedAt, user.UpdatedAt);

    private static object ToAuditValue(User user) => new
    {
        empno = user.Empno,
        name = user.Name,
        email = user.Email,
        dept_id = user.DeptId,
        role = user.Role,
        is_active = user.IsActive,
        notify_email_enabled = user.NotifyEmailEnabled
    };

    private static Dictionary<string, string[]> FieldError(string field, string message) =>
        new(StringComparer.Ordinal) { [field] = [message] };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
