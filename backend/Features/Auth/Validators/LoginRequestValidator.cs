using FluentValidation;
using IsoDocument.Api.Features.Auth.Dtos;

namespace IsoDocument.Api.Features.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Empno)
            .NotEmpty()
            .WithMessage("請輸入帳號。")
            .OverridePropertyName("empno");
        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("請輸入密碼。")
            .OverridePropertyName("password");
    }
}
