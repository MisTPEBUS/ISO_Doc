namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record AttachmentResponse(
    Guid AttachmentId,
    string AttachmentNo,
    string Name,
    bool HasFile);

public sealed record CreatedAttachmentResponse(
    Guid AttachmentId,
    string AttachmentNo,
    bool HasFile);

public sealed record CreateAttachmentsResponse(
    IReadOnlyList<CreatedAttachmentResponse> Created);
