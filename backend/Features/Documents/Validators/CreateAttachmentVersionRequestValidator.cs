using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class CreateAttachmentVersionRequestValidator
    : AbstractValidator<CreateAttachmentVersionRequest>
{
    public CreateAttachmentVersionRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.ChangeType)
            .Must(value => value is "MAJOR" or "MINOR")
            .WithMessage("變更類型必須是 MAJOR 或 MINOR。")
            .OverridePropertyName("changeType");
        RuleFor(request => request.EffectiveDate)
            .NotNull()
            .WithMessage("請輸入生效日期。")
            .Must(value => !value.HasValue || value.Value >= Today(timeProvider))
            .WithMessage("生效日期不可早於發佈日期。")
            .OverridePropertyName("effectiveDate");
        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("請上傳附件檔案。")
            .Must(file => file is null || AttachmentFileRules.HasAllowedExtension(file.FileName))
            .WithMessage("不允許的附件檔案類型。")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("原始檔名不可超過 255 個字元。")
            .OverridePropertyName("file");
    }

    private static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
