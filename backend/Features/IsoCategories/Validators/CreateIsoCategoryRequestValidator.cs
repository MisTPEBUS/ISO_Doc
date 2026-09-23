using FluentValidation;
using IsoDocument.Api.Features.IsoCategories.Dtos;

namespace IsoDocument.Api.Features.IsoCategories.Validators;

public sealed class CreateIsoCategoryRequestValidator : AbstractValidator<CreateIsoCategoryRequest>
{
    public CreateIsoCategoryRequestValidator(IIsoCategoryStore isoCategoryStore)
    {
        RuleFor(request => request.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("請選擇公司。")
            .MustAsync(isoCategoryStore.CompanyExistsAsync)
            .WithMessage("指定的公司不存在。")
            .OverridePropertyName("companyId");

        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("請輸入分類名稱。")
            .MaximumLength(100)
            .WithMessage("分類名稱不可超過 100 個字元。")
            .OverridePropertyName("name");
    }
}
