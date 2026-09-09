using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class CreateAttachmentsRequestValidator
    : AbstractValidator<CreateAttachmentsRequest>
{
    public CreateAttachmentsRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .WithMessage("At least one attachment is required.")
            .OverridePropertyName("items");
        RuleForEach(request => request.Items)
            .SetValidator(new CreateAttachmentItemRequestValidator());
    }
}

public sealed class CreateAttachmentItemRequestValidator
    : AbstractValidator<CreateAttachmentItemRequest>
{
    public CreateAttachmentItemRequestValidator()
    {
        RuleFor(item => item.AttachmentNo)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Attachment number is required.")
            .MaximumLength(50)
            .WithMessage("Attachment number must not exceed 50 characters.");
        RuleFor(item => item.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Attachment name is required.")
            .MaximumLength(255)
            .WithMessage("Attachment name must not exceed 255 characters.");
        RuleFor(item => item.File)
            .Must(file => file is null || AttachmentFileRules.HasAllowedExtension(file.FileName))
            .WithMessage("The attachment file extension is not allowed.")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("The original file name must not exceed 255 characters.");
    }
}
