namespace IsoDocument.Api.Features.Auth.Dtos;

public sealed record MeResponse(
    Guid UserId,
    string Empno,
    string Name,
    string Role,
    Guid CompanyId,
    Guid DeptId,
    bool MustChangePassword);
