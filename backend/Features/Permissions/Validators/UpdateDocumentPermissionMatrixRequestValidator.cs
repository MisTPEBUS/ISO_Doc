using FluentValidation;
using IsoDocument.Api.Features.Permissions.Dtos;

namespace IsoDocument.Api.Features.Permissions.Validators;

public sealed class UpdateDocumentPermissionMatrixRequestValidator
    : AbstractValidator<UpdateDocumentPermissionMatrixRequest>
{
    public UpdateDocumentPermissionMatrixRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .WithMessage("至少需要一筆異動。")
            .Must(items => items.Select(item => item.DocumentId).Distinct().Count() == items.Count)
            .WithMessage("documentId 不可重複。")
            .OverridePropertyName("items");

        RuleForEach(request => request.Items)
            .SetValidator(new UpdateDocumentPermissionMatrixItemRequestValidator());
    }
}

public sealed class UpdateDocumentPermissionMatrixItemRequestValidator
    : AbstractValidator<UpdateDocumentPermissionMatrixItemRequest>
{
    public UpdateDocumentPermissionMatrixItemRequestValidator()
    {
        RuleFor(item => item.DepartmentIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("同一份文件的 departmentIds 不可重複。");
    }
}
