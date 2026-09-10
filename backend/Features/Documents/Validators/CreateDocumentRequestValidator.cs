using System.Text.RegularExpressions;
using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed partial class CreateDocumentRequestValidator
    : AbstractValidator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator(IDocumentStore documentStore)
    {
        RuleFor(request => request.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("請選擇公司。")
            .MustAsync(documentStore.CompanyExistsAsync)
            .WithMessage("指定的公司不存在。")
            .OverridePropertyName("companyId");
        RuleFor(request => request.DocumentNo)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入文件編號。")
            .MaximumLength(50)
            .WithMessage("文件編號不可超過 50 個字元。")
            .Must(value => value is not null && DocumentNoPattern().IsMatch(value.Trim()))
            .WithMessage("文件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字。")
            .OverridePropertyName("documentNo");
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("請輸入文件名稱。")
            .MaximumLength(255)
            .WithMessage("文件名稱不可超過 255 個字元。")
            .OverridePropertyName("name");
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,48}[A-Za-z0-9])?$")]
    private static partial Regex DocumentNoPattern();
}
