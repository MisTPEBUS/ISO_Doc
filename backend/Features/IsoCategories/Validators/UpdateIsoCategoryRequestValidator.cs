using FluentValidation;
using IsoDocument.Api.Features.IsoCategories.Dtos;

namespace IsoDocument.Api.Features.IsoCategories.Validators;

public sealed class UpdateIsoCategoryRequestValidator : AbstractValidator<UpdateIsoCategoryRequest>
{
    public UpdateIsoCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("請輸入分類名稱。")
            .MaximumLength(100)
            .WithMessage("分類名稱不可超過 100 個字元。")
            .OverridePropertyName("name");
    }
}
