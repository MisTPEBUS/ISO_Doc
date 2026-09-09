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
            .WithMessage("Company is required.")
            .MustAsync(deptStore.CompanyExistsAsync)
            .WithMessage("The specified company does not exist.")
            .OverridePropertyName("companyId");

        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Department name is required.")
            .MaximumLength(100)
            .WithMessage("Department name must not exceed 100 characters.")
            .OverridePropertyName("name");
    }
}
