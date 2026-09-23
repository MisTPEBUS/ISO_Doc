using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

public interface IDocumentStore
{
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);
    Task<bool> DocumentNoExistsAsync(
        Guid companyId, string documentNo, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid? companyId, string? keyword, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> ListAsync(
        Guid? companyId, string? keyword, int skip, int take,
        CancellationToken cancellationToken);
    Task<Document?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentAttachmentRecord>> ListAttachmentsAsync(
        Guid documentId, CancellationToken cancellationToken);
    /// <summary>
    /// 該公司底下全部部門的 id，用於文件建立時預設全開 document_dept_permissions。
    /// depts 沒有 is_active 欄位，因此不分啟用／停用，回傳該公司全部部門。
    /// </summary>
    Task<IReadOnlyList<Guid>> ListCompanyDeptIdsAsync(
        Guid companyId, CancellationToken cancellationToken);
    Task<bool> IsoCategoryBelongsToCompanyAsync(
        Guid companyId, Guid isoCategoryId, CancellationToken cancellationToken);
    /// <summary>
    /// 發行單位需屬於文件所在公司；找不到或不屬於該公司時回傳 null。
    /// 同時回傳 Dept 實體（而非單純 bool），讓呼叫端不必再多查一次就能取得 dept.Name。
    /// </summary>
    Task<Dept?> FindCompanyDeptAsync(
        Guid companyId, Guid deptId, CancellationToken cancellationToken);
    void Add(Document document);
    void Add(DocumentVersion version);
    void AddRange(IEnumerable<DocumentDeptPermission> permissions);
    void Detach(Document document);
    void Detach(DocumentVersion version);
    void DetachRange(IEnumerable<DocumentDeptPermission> permissions);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IDocumentTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public sealed record DocumentAttachmentRecord(
    Attachment Attachment,
    AttachmentVersion? CurrentVersion);

public interface IDocumentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
