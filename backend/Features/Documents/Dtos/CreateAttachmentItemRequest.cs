using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class CreateAttachmentItemRequest
{
    public string? AttachmentNo { get; init; }
    public string? Name { get; init; }
    public IFormFile? File { get; init; }
}
