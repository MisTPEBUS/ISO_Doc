using FluentValidation;
using IsoDocument.Api.Features.Depts.Dtos;

namespace IsoDocument.Api.Features.Depts.Validators;

public sealed class UpdateDeptRequestValidator : AbstractValidator<UpdateDeptRequest>
{
    public UpdateDeptRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Department name is required.")
            .MaximumLength(100)
            .WithMessage("Department name must not exceed 100 characters.")
            .OverridePropertyName("name");
    }
}
