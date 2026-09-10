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
            .WithMessage("至少需要一個附件。")
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
            .WithMessage("請輸入附件編號。")
            .MaximumLength(50)
            .WithMessage("附件編號不可超過 50 個字元。");
        RuleFor(item => item.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入附件名稱。")
            .MaximumLength(255)
            .WithMessage("附件名稱不可超過 255 個字元。");
        RuleFor(item => item.File)
            .Must(file => file is null || AttachmentFileRules.HasAllowedExtension(file.FileName))
            .WithMessage("不允許的附件檔案類型。")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("原始檔名不可超過 255 個字元。");
    }
}
