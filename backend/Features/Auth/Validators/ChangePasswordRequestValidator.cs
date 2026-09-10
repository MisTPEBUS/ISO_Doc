using FluentValidation;
using IsoDocument.Api.Features.Auth.Dtos;

namespace IsoDocument.Api.Features.Auth.Validators;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .WithMessage("請輸入目前密碼。")
            .OverridePropertyName("currentPassword");
        RuleFor(request => request.NewPassword)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入新密碼。")
            .OverridePropertyName("newPassword");
        RuleFor(request => request.NewPasswordConfirmation)
            .NotEmpty()
            .WithMessage("請輸入確認密碼。")
            .Equal(request => request.NewPassword)
            .WithMessage("確認密碼與新密碼不符。")
            .OverridePropertyName("newPasswordConfirmation");
    }
}
