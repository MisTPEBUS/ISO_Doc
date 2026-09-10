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
            .WithMessage("請輸入使用者姓名。")
            .MaximumLength(100)
            .WithMessage("使用者姓名不可超過 100 個字元。")
            .OverridePropertyName("name");
        RuleFor(request => request.Email)
            .MaximumLength(255)
            .WithMessage("電子郵件不可超過 255 個字元。")
            .EmailAddress()
            .WithMessage("電子郵件格式不正確。")
            .When(request => !string.IsNullOrWhiteSpace(request.Email))
            .OverridePropertyName("email");
        RuleFor(request => request.DeptId)
            .NotEmpty()
            .WithMessage("請選擇部門。")
            .OverridePropertyName("deptId");
        RuleFor(request => request.Role)
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: false, out _))
            .WithMessage("角色必須是 USER、COMPANY_ADMIN 或 SYSTEM_ADMIN。")
            .OverridePropertyName("role");
    }
}
