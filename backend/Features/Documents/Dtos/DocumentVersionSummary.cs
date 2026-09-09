namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentVersionSummary(
    string Version,
    string Status,
    DateOnly? EffectiveDate,
    DateOnly? ExpiredDate);
