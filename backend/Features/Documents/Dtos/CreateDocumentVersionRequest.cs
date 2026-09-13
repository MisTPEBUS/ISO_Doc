using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class CreateDocumentVersionRequest
{
    public string? Version { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public int? PageCount { get; init; }
    public string? Memo { get; init; }
    public IFormFile? File { get; init; }
}
