namespace IsoDocument.Api.Features.Users.Dtos;

public sealed record UserResponse(
    Guid Id,
    Guid CompanyId,
    Guid DeptId,
    string Empno,
    string Name,
    string? Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    bool NotifyEmailEnabled,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
