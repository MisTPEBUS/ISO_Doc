using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class UploadDocumentVersionFileRequest
{
    public IFormFile? File { get; init; }
    public DateOnly? EffectiveDate { get; init; }
}
