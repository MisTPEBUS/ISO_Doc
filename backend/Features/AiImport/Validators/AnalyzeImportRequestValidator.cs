using FluentValidation;
using IsoDocument.Api.Features.AiImport.Dtos;

namespace IsoDocument.Api.Features.AiImport.Validators;

public sealed class AnalyzeImportRequestValidator : AbstractValidator<AnalyzeImportRequest>
{
    private const int MaximumFileCount = 500;

    public AnalyzeImportRequestValidator()
    {
        RuleFor(request => request.CompanyId)
            .NotEmpty()
            .WithMessage("請選擇公司。")
            .OverridePropertyName("companyId");
        RuleFor(request => request.Files)
            .Must(files => files is { Count: > 0 })
            .WithMessage("請至少提供一個檔案。")
            .Must(files => files is null || files.Count <= MaximumFileCount)
            .WithMessage($"一次最多可分析 {MaximumFileCount} 個檔案。")
            .OverridePropertyName("files");
    }
}
