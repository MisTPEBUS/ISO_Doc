namespace IsoDocument.Api.Features.Home.Dtos;

public sealed record AvailableDocumentResponse(
    Guid DocumentId,
    string DocumentNo,
    string Name,
    string CompanyName,
    AvailableDocumentVersionResponse CurrentVersion);

public sealed record AvailableDocumentVersionResponse(
    Guid VersionId,
    string Version,
    DateOnly? EffectiveDate,
    int? PageCount);
