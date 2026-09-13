namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record AttachmentDetailResponse(
    Guid AttachmentId,
    string AttachmentNo,
    string Name,
    bool IsActive,
    IReadOnlyList<AttachmentVersionSummary> Versions);
