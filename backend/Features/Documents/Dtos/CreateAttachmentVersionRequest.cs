using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class CreateAttachmentVersionRequest
{
    public string? Version { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public IFormFile? File { get; init; }
}
