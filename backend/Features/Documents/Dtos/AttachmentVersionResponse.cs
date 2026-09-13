namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record AttachmentVersionResponse(Guid VersionId, string Version, string Status);

public sealed record AttachmentVersionDetailResponse(
    Guid VersionId,
    string Version,
    string Status,
    DateOnly? EffectiveDate,
    DateOnly? ExpiredDate,
    bool HasFile);
