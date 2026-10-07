namespace IsoDocument.Api.Features.Depts;

/// <summary><c>GET /api/depts</c> 可用的 sortBy 值（未帶時依 seq、名稱升冪，seq 為空者在後）。</summary>
public static class DeptSortFields
{
    public const string Name = "name";
    public const string Seq = "seq";
    public const string UpdatedAt = "updatedAt";

    public static readonly IReadOnlyList<string> All = [Name, Seq, UpdatedAt];
}
