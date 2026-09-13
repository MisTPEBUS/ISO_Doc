using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class CreateAttachmentVersionRequestValidator
    : AbstractValidator<CreateAttachmentVersionRequest>
{
    public CreateAttachmentVersionRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.Version)
            .NotEmpty()
            .WithMessage("請輸入版本號。")
            .Must(value => DocumentVersionNumber.TryParse(value, out _, out _, out _))
            .WithMessage("版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。")
            .OverridePropertyName("version");
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
