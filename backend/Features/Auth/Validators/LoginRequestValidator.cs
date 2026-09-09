using FluentValidation;
using IsoDocument.Api.Features.Auth.Dtos;

namespace IsoDocument.Api.Features.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Empno)
            .NotEmpty()
            .WithMessage("Employee number is required.")
            .OverridePropertyName("empno");
        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .OverridePropertyName("password");
    }
}
