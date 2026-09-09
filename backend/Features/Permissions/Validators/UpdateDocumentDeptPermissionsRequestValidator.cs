using FluentValidation;
using IsoDocument.Api.Features.Permissions.Dtos;

namespace IsoDocument.Api.Features.Permissions.Validators;

public sealed class UpdateDocumentDeptPermissionsRequestValidator
    : AbstractValidator<UpdateDocumentDeptPermissionsRequest>
{
    public UpdateDocumentDeptPermissionsRequestValidator()
    {
        RuleFor(request => request.DeptIds)
            .NotNull()
            .WithMessage("Department ids are required.")
            .NotEmpty()
            .WithMessage("At least one department id is required.")
            .OverridePropertyName("deptIds");
    }
}
