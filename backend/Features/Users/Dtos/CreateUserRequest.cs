namespace IsoDocument.Api.Features.Users.Dtos;

public sealed record CreateUserRequest(
    string? Empno,
    string? Name,
    string? Email,
    Guid CompanyId,
    Guid DeptId,
    string? Role,
    string? Password,
    string? PasswordConfirmation);
