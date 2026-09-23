using System.Text.RegularExpressions;
using FluentValidation;
using IsoDocument.Api.Features.AiImport.Dtos;
using IsoDocument.Api.Features.Documents;

namespace IsoDocument.Api.Features.AiImport.Validators;

public sealed partial class CommitImportAttachmentItemValidator
    : AbstractValidator<CommitImportAttachmentItem>
{
    public CommitImportAttachmentItemValidator()
    {
        // 表單及附件編號可留空：部分掃描進來的檔案本來就沒有編號規則，留空一律視為新增表單及附件。
        RuleFor(item => item.AttachmentNo)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(100)
            .WithMessage("表單及附件編號不可超過 100 個字元。")
            .Must(value => value is null || AttachmentNoPattern().IsMatch(value.Trim()))
            .WithMessage("表單及附件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字。")
            .When(item => !string.IsNullOrWhiteSpace(item.AttachmentNo))
            .OverridePropertyName("attachmentNo");
        RuleFor(item => item.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入表單及附件名稱。")
            .MaximumLength(255)
            .WithMessage("表單及附件名稱不可超過 255 個字元。")
            .OverridePropertyName("name");
        RuleFor(item => item.Version)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入版本號。")
            .Must(value => DocumentVersionNumber.TryParse(value, out _, out _, out _))
            .WithMessage("版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。")
            .When(item => item.File is not null)
            .OverridePropertyName("version");
        RuleFor(item => item.File)
            .Must(file => file is null || IsAllowedExtension(file.FileName))
            .WithMessage("表單及附件副檔名須為 jpg、jpeg、png、pdf、doc、docx、xls、xlsx、odt、ods 之一。")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("原始檔名不可超過 255 個字元。")
            .OverridePropertyName("file");
    }

    private static bool IsAllowedExtension(string fileName) =>
        AttachmentFileRules.HasAllowedExtension(fileName);

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,98}[A-Za-z0-9])?$")]
    private static partial Regex AttachmentNoPattern();
}
