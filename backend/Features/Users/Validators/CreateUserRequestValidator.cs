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
            .WithMessage("請輸入員工編號。")
            .MaximumLength(30)
            .WithMessage("員工編號不可超過 30 個字元。")
            .OverridePropertyName("empno");
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
        RuleFor(request => request.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("請選擇公司。")
            .MustAsync(userStore.CompanyExistsAsync)
            .WithMessage("指定的公司不存在。")
            .OverridePropertyName("companyId");
        RuleFor(request => request.DeptId)
            .NotEmpty()
            .WithMessage("請選擇部門。")
            .OverridePropertyName("deptId");
        RuleFor(request => request.Role)
            .Must(IsValidRole)
            .WithMessage("角色必須是 USER、COMPANY_ADMIN 或 SYSTEM_ADMIN。")
            .OverridePropertyName("role");
        RuleFor(request => request.Password)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("密碼不可空白。")
            .When(request => request.Password is not null)
            .OverridePropertyName("password");
        RuleFor(request => request.PasswordConfirmation)
            .Equal(request => request.Password)
            .WithMessage("確認密碼與密碼不符。")
            .OverridePropertyName("passwordConfirmation");
        RuleFor(request => request.PasswordConfirmation)
            .Null()
            .WithMessage("未設定密碼時不可填寫確認密碼。")
            .When(request => request.Password is null)
            .OverridePropertyName("passwordConfirmation");
    }

    private static bool IsValidRole(string? role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: false, out _);
}
