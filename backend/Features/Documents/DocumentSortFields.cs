namespace IsoDocument.Api.Features.Documents;

/// <summary><c>GET /api/documents</c> 可用的 sortBy 值（未帶時依文件編號升冪）。</summary>
public static class DocumentSortFields
{
    public const string IsActive = "isActive";
    public const string DocumentNo = "documentNo";
    public const string Name = "name";
    public const string IsoCategoryName = "isoCategoryName";
    public const string DeptName = "deptName";
    public const string UpdatedAt = "updatedAt";

    public static readonly IReadOnlyList<string> All =
        [IsActive, DocumentNo, Name, IsoCategoryName, DeptName, UpdatedAt];
}
