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
    void Add(Document document);
    void Add(DocumentVersion version);
    void Detach(Document document);
    void Detach(DocumentVersion version);
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
