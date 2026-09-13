namespace IsoDocument.Api.Data.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid DeptId { get; set; }
    public string Empno { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? PasswordDigest { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public bool NotifyEmailEnabled { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public int? LegacyUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
