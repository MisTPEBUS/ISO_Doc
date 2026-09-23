namespace IsoDocument.Api.Data.Entities;

public sealed class Document
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? IsoCategoryId { get; set; }
    public Guid? DeptId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 發行單位（可為 null）。僅在明確 Include 時才會載入，一般查詢預設不會自動帶出。
    /// </summary>
    public Dept? Dept { get; set; }
}
