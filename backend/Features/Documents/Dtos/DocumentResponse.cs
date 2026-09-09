namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentResponse(
    Guid Id,
    Guid CompanyId,
    string DocumentNo,
    string Name,
    bool IsActive,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
