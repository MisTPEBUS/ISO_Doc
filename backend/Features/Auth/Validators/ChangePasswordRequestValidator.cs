using FluentValidation;
using IsoDocument.Api.Features.Auth.Dtos;

namespace IsoDocument.Api.Features.Auth.Validators;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required.")
            .OverridePropertyName("currentPassword");
        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required.")
            .MinimumLength(8)
            .WithMessage("New password must be at least 8 characters.")
            .OverridePropertyName("newPassword");
        RuleFor(request => request.NewPasswordConfirmation)
            .NotEmpty()
            .WithMessage("Password confirmation is required.")
            .Equal(request => request.NewPassword)
            .WithMessage("Password confirmation does not match.")
            .OverridePropertyName("newPasswordConfirmation");
    }
}
