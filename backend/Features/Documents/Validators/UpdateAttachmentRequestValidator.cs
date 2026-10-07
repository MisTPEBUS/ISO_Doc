using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class UpdateAttachmentRequestValidator : AbstractValidator<UpdateAttachmentRequest>
{
    public UpdateAttachmentRequestValidator()
    {
        // 名稱規則需與 CreateAttachmentRequestValidator 一致。
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入表單及附件名稱。")
            .MaximumLength(255)
            .WithMessage("表單及附件名稱不可超過 255 個字元。")
            .OverridePropertyName("name");
    }
}
