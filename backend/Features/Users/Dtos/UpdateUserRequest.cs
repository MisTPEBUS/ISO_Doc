namespace IsoDocument.Api.Features.Users.Dtos;

public sealed record UpdateUserRequest(
    string? Name,
    string? Email,
    Guid DeptId,
    string? Role,
    bool IsActive,
    bool NotifyEmailEnabled);
