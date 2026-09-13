namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentDetailResponse(
    Guid Id,
    Guid CompanyId,
    string DocumentNo,
    string Name,
    bool IsActive,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DocumentVersionSummary? CurrentVersion,
    IReadOnlyList<DocumentVersionSummary> Versions);
