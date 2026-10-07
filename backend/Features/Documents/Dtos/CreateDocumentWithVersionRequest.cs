using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.Documents.Dtos;

/// <summary>
/// 單筆新增ISO管理程序：文件身份與第一個帶檔版本於同一請求送出。
/// 欄位規則分別沿用 <see cref="CreateDocumentRequest"/> 與 <see cref="CreateDocumentVersionRequest"/>。
/// </summary>
public sealed class CreateDocumentWithVersionRequest
{
    public Guid CompanyId { get; init; }
    public string? DocumentNo { get; init; }
    public string? Name { get; init; }
    public Guid? IsoCategoryId { get; init; }
    public Guid? DeptId { get; init; }
    public string? Version { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public int? PageCount { get; init; }
    public string? Memo { get; init; }
    public IFormFile? File { get; init; }
}
