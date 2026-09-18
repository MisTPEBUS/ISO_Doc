using System.Text.RegularExpressions;
using FluentValidation;
using IsoDocument.Api.Features.AiImport.Dtos;
using IsoDocument.Api.Features.Documents;

namespace IsoDocument.Api.Features.AiImport.Validators;

public sealed partial class CommitImportDocumentItemValidator
    : AbstractValidator<CommitImportDocumentItem>
{
    public CommitImportDocumentItemValidator()
    {
        RuleFor(item => item.DocumentNo)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入文件編號。")
            .MaximumLength(50)
            .WithMessage("文件編號不可超過 50 個字元。")
            .Must(value => value is not null && DocumentNoPattern().IsMatch(value.Trim()))
            .WithMessage("文件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字。")
            .OverridePropertyName("documentNo");
        RuleFor(item => item.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入文件名稱。")
            .MaximumLength(255)
            .WithMessage("文件名稱不可超過 255 個字元。")
            .OverridePropertyName("name");
        RuleFor(item => item.Version)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入版本號。")
            .Must(value => DocumentVersionNumber.TryParse(value, out _, out _, out _))
            .WithMessage("版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。")
            .When(item => item.MainFile is not null)
            .OverridePropertyName("version");
        RuleFor(item => item.EffectiveDate)
            .NotNull()
            .WithMessage("請輸入生效日期。")
            .When(item => item.MainFile is not null)
            .OverridePropertyName("effectiveDate");
        RuleFor(item => item.PageCount)
            .GreaterThan(0)
            .When(item => item.PageCount.HasValue)
            .WithMessage("頁數必須大於零。")
            .OverridePropertyName("pageCount");
        RuleFor(item => item.MainFile)
            .Must(file => file is null || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("文件檔案的副檔名必須是 .pdf。")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("原始檔名不可超過 255 個字元。")
            .OverridePropertyName("mainFile");
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,48}[A-Za-z0-9])?$")]
    private static partial Regex DocumentNoPattern();
}
