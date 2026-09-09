namespace IsoDocument.Api.Features.Auth.Dtos;

public sealed record LoginResponse(Guid UserId, string Name, string Role, Guid CompanyId);
