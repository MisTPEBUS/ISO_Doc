namespace IsoDocument.Api.Features.Home;

/// <summary><c>GET /api/documents/available</c> 可用的 sortBy 值（未帶時依文件編號升冪）。</summary>
public static class AvailableDocumentSortFields
{
    public const string DocumentNo = "documentNo";
    public const string Name = "name";
    public const string IsoCategoryName = "isoCategoryName";
    public const string DeptName = "deptName";
    public const string CompanyName = "companyName";
    public const string Version = "version";
    public const string EffectiveDate = "effectiveDate";

    public static readonly IReadOnlyList<string> All =
        [DocumentNo, Name, IsoCategoryName, DeptName, CompanyName, Version, EffectiveDate];
}
