namespace IsoDocument.Api.Features.IsoCategories;

/// <summary><c>GET /api/iso-categories</c> 可用的 sortBy 值（未帶時依名稱升冪）。</summary>
public static class IsoCategorySortFields
{
    public const string Name = "name";
    public const string IsActive = "isActive";
    public const string UpdatedAt = "updatedAt";

    public static readonly IReadOnlyList<string> All = [Name, IsActive, UpdatedAt];
}
