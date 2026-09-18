using FluentValidation;
using IsoDocument.Api.Features.AiImport.Dtos;

namespace IsoDocument.Api.Features.AiImport.Validators;

/// <summary>
/// 只驗證整個批次的外層結構；每一筆主文/附件的欄位規則交給
/// <see cref="CommitImportDocumentItemValidator"/> / <see cref="CommitImportAttachmentItemValidator"/>
/// 在 service 逐筆處理時執行（比照 BulkImportAsync 的慣例），
/// 讓單筆失敗可以個別回報，不會讓整批 400。
/// </summary>
public sealed class CommitImportRequestValidator : AbstractValidator<CommitImportRequest>
{
    private const int MaximumDocumentCount = 200;

    public CommitImportRequestValidator()
    {
        RuleFor(request => request.CompanyId)
            .NotEmpty()
            .WithMessage("請選擇公司。")
            .OverridePropertyName("companyId");
        RuleFor(request => request.Documents)
            .Must(documents => documents is { Count: > 0 })
            .WithMessage("請至少提供一筆主文資料。")
            .Must(documents => documents is null || documents.Count <= MaximumDocumentCount)
            .WithMessage($"一次最多可匯入 {MaximumDocumentCount} 筆主文。")
            .OverridePropertyName("documents");
    }
}
