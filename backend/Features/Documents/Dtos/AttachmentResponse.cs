namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record AttachmentResponse(
    Guid AttachmentId,
    string AttachmentNo,
    string Name,
    bool IsActive);
