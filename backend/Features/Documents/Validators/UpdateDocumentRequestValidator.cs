using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class UpdateDocumentRequestValidator : AbstractValidator<UpdateDocumentRequest>
{
    public UpdateDocumentRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Document name is required.")
            .MaximumLength(255)
            .WithMessage("Document name must not exceed 255 characters.")
            .OverridePropertyName("name");
    }
}
