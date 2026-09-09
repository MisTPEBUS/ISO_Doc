using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class CreateDocumentVersionRequestValidator
    : AbstractValidator<CreateDocumentVersionRequest>
{
    public CreateDocumentVersionRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.ChangeType)
            .Must(value => value is "MAJOR" or "MINOR")
            .WithMessage("Change type must be MAJOR or MINOR.")
            .OverridePropertyName("changeType");
        RuleFor(request => request.EffectiveDate)
            .NotNull()
            .WithMessage("Effective date is required.")
            .Must(value => !value.HasValue || value.Value >= Today(timeProvider))
            .WithMessage("Effective date must not be earlier than the publish date.")
            .OverridePropertyName("effectiveDate");
        RuleFor(request => request.PageCount)
            .GreaterThan(0)
            .When(request => request.PageCount.HasValue)
            .WithMessage("Page count must be greater than zero.")
            .OverridePropertyName("pageCount");
        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("A PDF file is required.")
            .Must(file => file is null
                || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("The document file must have a .pdf extension.")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("The original file name must not exceed 255 characters.")
            .OverridePropertyName("file");
    }

    private static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
