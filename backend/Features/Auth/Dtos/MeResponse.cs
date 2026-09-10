namespace IsoDocument.Api.Features.Auth.Dtos;

public sealed record MeResponse(
    Guid UserId,
    string Empno,
    string Name,
    string Role,
    Guid CompanyId,
    string CompanyName,
    Guid DeptId,
    string DeptName,
    bool MustChangePassword);
