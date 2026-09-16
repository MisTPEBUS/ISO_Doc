using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class UploadDocumentVersionFileRequestValidator
    : AbstractValidator<UploadDocumentVersionFileRequest>
{
    public UploadDocumentVersionFileRequestValidator()
    {
        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("請上傳 PDF 檔案。")
            .Must(file => file is null
                || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("文件檔案的副檔名必須是 .pdf。")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("原始檔名不可超過 255 個字元。")
            .OverridePropertyName("file");
    }
}
