namespace IsoDocument.Api.Features.Users;

/// <summary><c>GET /api/users</c> 可用的 sortBy 值（未帶時依帳號升冪）。</summary>
public static class UserSortFields
{
    public const string Empno = "empno";
    public const string Name = "name";
    public const string DeptName = "deptName";
    public const string Email = "email";
    public const string Role = "role";
    public const string IsActive = "isActive";
    public const string LastLoginAt = "lastLoginAt";

    public static readonly IReadOnlyList<string> All =
        [Empno, Name, DeptName, Email, Role, IsActive, LastLoginAt];
}
