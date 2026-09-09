using FluentValidation;
using IsoDocument.Api.Features.Users.Dtos;
using IsoDocument.Api.Security;

namespace IsoDocument.Api.Features.Users.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("User name is required.")
            .MaximumLength(100)
            .WithMessage("User name must not exceed 100 characters.")
            .OverridePropertyName("name");
        RuleFor(request => request.Email)
            .MaximumLength(255)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email))
            .OverridePropertyName("email");
        RuleFor(request => request.DeptId)
            .NotEmpty()
            .WithMessage("Department is required.")
            .OverridePropertyName("deptId");
        RuleFor(request => request.Role)
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: false, out _))
            .WithMessage("Role must be USER, COMPANY_ADMIN, or SYSTEM_ADMIN.")
            .OverridePropertyName("role");
    }
}
