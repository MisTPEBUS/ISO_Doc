namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentVersionSummary(
    Guid VersionId,
    string Version,
    string Status,
    DateOnly? PublishDate,
    DateOnly? EffectiveDate,
    DateOnly? ExpiredDate,
    int? PageCount,
    bool HasFile);
