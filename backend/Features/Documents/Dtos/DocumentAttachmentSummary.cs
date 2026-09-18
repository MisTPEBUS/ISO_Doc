namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentAttachmentSummary(
    Guid AttachmentId,
    string? AttachmentNo,
    string Name,
    bool IsActive,
    AttachmentVersionSummary? CurrentVersion);
