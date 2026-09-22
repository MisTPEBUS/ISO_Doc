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
