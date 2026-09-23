using System.Text.RegularExpressions;
using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed partial class CreateAttachmentRequestValidator
    : AbstractValidator<CreateAttachmentRequest>
{
    public CreateAttachmentRequestValidator()
    {
        // 表單及附件編號可留空：部分掃描進來的檔案本來就沒有編號規則，留空一律視為新增表單及附件
        // （比照 AiImport 的 CommitImportAttachmentItemValidator，兩邊規則需一致）。
        RuleFor(request => request.AttachmentNo)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(100)
            .WithMessage("表單及附件編號不可超過 100 個字元。")
            .Must(value => value is null || AttachmentNoPattern().IsMatch(value.Trim()))
            .WithMessage("表單及附件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字。")
            .When(request => !string.IsNullOrWhiteSpace(request.AttachmentNo))
            .OverridePropertyName("attachmentNo");
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入表單及附件名稱。")
            .MaximumLength(255)
            .WithMessage("表單及附件名稱不可超過 255 個字元。")
            .OverridePropertyName("name");
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,98}[A-Za-z0-9])?$")]
    private static partial Regex AttachmentNoPattern();
}
