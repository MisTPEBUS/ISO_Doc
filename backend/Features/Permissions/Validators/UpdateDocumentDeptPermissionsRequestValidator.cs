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
            .WithMessage("請提供部門清單。")
            .NotEmpty()
            .WithMessage("至少需要一個部門。")
            .OverridePropertyName("deptIds");
    }
}
