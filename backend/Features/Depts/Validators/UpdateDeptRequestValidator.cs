using FluentValidation;
using IsoDocument.Api.Features.Depts.Dtos;

namespace IsoDocument.Api.Features.Depts.Validators;

public sealed class UpdateDeptRequestValidator : AbstractValidator<UpdateDeptRequest>
{
    public UpdateDeptRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("請輸入部門名稱。")
            .MaximumLength(100)
            .WithMessage("部門名稱不可超過 100 個字元。")
            .OverridePropertyName("name");
    }
}
