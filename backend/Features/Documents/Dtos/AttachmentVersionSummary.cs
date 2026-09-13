namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record AttachmentVersionSummary(
    string Version,
    string Status,
    DateOnly? EffectiveDate,
    DateOnly? ExpiredDate);
