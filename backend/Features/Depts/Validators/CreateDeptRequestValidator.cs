using FluentValidation;
using IsoDocument.Api.Features.Depts.Dtos;

namespace IsoDocument.Api.Features.Depts.Validators;

public sealed class CreateDeptRequestValidator : AbstractValidator<CreateDeptRequest>
{
    public CreateDeptRequestValidator(IDeptStore deptStore)
    {
        RuleFor(request => request.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("請選擇公司。")
            .MustAsync(deptStore.CompanyExistsAsync)
            .WithMessage("指定的公司不存在。")
            .OverridePropertyName("companyId");

        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("請輸入部門名稱。")
            .MaximumLength(100)
            .WithMessage("部門名稱不可超過 100 個字元。")
            .OverridePropertyName("name");
    }
}
