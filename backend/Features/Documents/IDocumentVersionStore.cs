using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

public interface IDocumentVersionStore
{
    Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<string?> FindCompanyCodeAsync(Guid companyId, CancellationToken cancellationToken);
    Task<DocumentVersion?> FindVersionAsync(Guid versionId, CancellationToken cancellationToken);
    Task<DocumentVersion?> FindLatestVersionAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentVersion>> ListPublishedVersionsAsync(
        Guid documentId, CancellationToken cancellationToken);
    void Add(DocumentVersion version);
    void Remove(DocumentVersion version);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IDocumentVersionTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IDocumentVersionTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
