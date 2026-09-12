using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class UploadAttachmentFileRequest
{
    public IFormFile? File { get; init; }
}

public sealed record UploadAttachmentFileResponse(Guid AttachmentId, bool HasFile);
