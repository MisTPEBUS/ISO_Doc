namespace IsoDocument.Api.Features.Permissions.Dtos;

public sealed record DocumentPermissionMatrixQuery
{
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// 公司代碼篩選（companies.code）。僅在未提供 companyId 時採用，會先解析為 companyId。
    /// COMPANY_ADMIN 一律以自己公司為範圍，此參數對其無效。
    /// </summary>
    public string? CompanyCode { get; init; }

    public string? Keyword { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
