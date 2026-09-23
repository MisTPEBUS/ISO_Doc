namespace IsoDocument.Api.Features.IsoCategories.Dtos;

public sealed record CreateIsoCategoryRequest(Guid CompanyId, string? Name);
