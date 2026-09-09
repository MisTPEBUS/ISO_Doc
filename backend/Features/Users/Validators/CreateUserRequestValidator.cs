using FluentValidation;
using IsoDocument.Api.Features.Users.Dtos;
using IsoDocument.Api.Security;

namespace IsoDocument.Api.Features.Users.Validators;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserStore userStore)
    {
        RuleFor(request => request.Empno)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Employee number is required.")
            .MaximumLength(30)
            .WithMessage("Employee number must not exceed 30 characters.")
            .OverridePropertyName("empno");
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
        RuleFor(request => request.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Company is required.")
            .MustAsync(userStore.CompanyExistsAsync)
            .WithMessage("The specified company does not exist.")
            .OverridePropertyName("companyId");
        RuleFor(request => request.DeptId)
            .NotEmpty()
            .WithMessage("Department is required.")
            .OverridePropertyName("deptId");
        RuleFor(request => request.Role)
            .Must(IsValidRole)
            .WithMessage("Role must be USER, COMPANY_ADMIN, or SYSTEM_ADMIN.")
            .OverridePropertyName("role");
        RuleFor(request => request.Password)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Password must not be blank.")
            .When(request => request.Password is not null)
            .OverridePropertyName("password");
        RuleFor(request => request.PasswordConfirmation)
            .Equal(request => request.Password)
            .WithMessage("Password confirmation does not match.")
            .OverridePropertyName("passwordConfirmation");
        RuleFor(request => request.PasswordConfirmation)
            .Null()
            .WithMessage("Password confirmation requires a password.")
            .When(request => request.Password is null)
            .OverridePropertyName("passwordConfirmation");
    }

    private static bool IsValidRole(string? role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: false, out _);
}
