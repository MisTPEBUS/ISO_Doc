using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Security;
using Microsoft.AspNetCore.Identity;

namespace IsoDocument.Api.Features.Auth;

public sealed class AuthService(
    IAuthUserStore userStore,
    IPasswordHasher<User> passwordHasher,
    IAuthSession authSession,
    ICurrentUser currentUser,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    TimeProvider timeProvider) : IAuthService
{
    private const string InvalidCredentialsMessage = "The employee number or password is incorrect.";

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<LoginResponse>.ValidationFailed(ToErrors(validation));
        }

        var empno = request.Empno!.Trim();
        if (empno.Length == 0)
        {
            return Result<LoginResponse>.ValidationFailed(new Dictionary<string, string[]>
            {
                ["empno"] = ["Employee number is required."]
            });
        }

        var user = await userStore.FindByEmpnoAsync(empno, cancellationToken);
        if (user is null || !user.IsActive || user.PasswordDigest is null)
        {
            return Result<LoginResponse>.Unauthorized(InvalidCredentialsMessage);
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordDigest,
            request.Password!);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Result<LoginResponse>.Unauthorized(InvalidCredentialsMessage);
        }

        var now = timeProvider.GetUtcNow();
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordDigest = passwordHasher.HashPassword(user, request.Password!);
        }

        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await userStore.SaveChangesAsync(cancellationToken);
        await authSession.SignInAsync(user);

        return Result<LoginResponse>.Success(new LoginResponse(
            user.Id,
            user.Name,
            user.Role,
            user.CompanyId));
    }

    public async Task<Result> LogoutAsync()
    {
        await authSession.SignOutAsync();
        return Result.Success();
    }

    public async Task<Result<MeResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<MeResponse>.Unauthorized("Authentication is required.");
        }

        var user = await userStore.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<MeResponse>.Unauthorized("Authentication is required.");
        }

        authSession.IssueAntiforgeryToken();
        return Result<MeResponse>.Success(new MeResponse(
            user.Id,
            user.Empno,
            user.Name,
            user.Role,
            user.CompanyId,
            user.DeptId,
            user.MustChangePassword));
    }

    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.ValidationFailed(ToErrors(validation));
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var user = await userStore.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.PasswordDigest is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var currentPasswordVerification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordDigest,
            request.CurrentPassword!);
        if (currentPasswordVerification == PasswordVerificationResult.Failed)
        {
            return Result.ValidationFailed(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["The current password is incorrect."]
            });
        }

        var newPasswordVerification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordDigest,
            request.NewPassword!);
        if (newPasswordVerification != PasswordVerificationResult.Failed)
        {
            return Result.ValidationFailed(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["The new password must be different from the current password."]
            });
        }

        user.PasswordDigest = passwordHasher.HashPassword(user, request.NewPassword!);
        user.MustChangePassword = false;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await userStore.SaveChangesAsync(cancellationToken);
        await authSession.SignInAsync(user);

        return Result.Success();
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}
