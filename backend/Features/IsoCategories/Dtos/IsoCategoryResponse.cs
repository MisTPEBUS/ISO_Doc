namespace IsoDocument.Api.Features.IsoCategories.Dtos;

public sealed record IsoCategoryResponse(
    Guid Id,
    Guid CompanyId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
