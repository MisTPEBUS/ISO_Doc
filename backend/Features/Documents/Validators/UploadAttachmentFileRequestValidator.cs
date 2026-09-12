using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed class UploadAttachmentFileRequestValidator
    : AbstractValidator<UploadAttachmentFileRequest>
{
    public UploadAttachmentFileRequestValidator()
    {
        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("請選擇要上傳的檔案。")
            .Must(file => file is null || AttachmentFileRules.HasAllowedExtension(file.FileName))
            .WithMessage("不允許的附件檔案類型。")
            .Must(file => file is null || file.FileName.Length <= 255)
            .WithMessage("原始檔名不可超過 255 個字元。")
            .OverridePropertyName("file");
    }
}
