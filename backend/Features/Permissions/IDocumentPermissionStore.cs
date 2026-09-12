using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Permissions;

public interface IDocumentPermissionStore
{
    Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, Document>> FindDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentDeptPermission>> ListPermissionsAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentDeptPermission>>> ListPermissionsAsync(
        IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> FindCompanyDeptIdsAsync(
        Guid companyId, IReadOnlyCollection<Guid> deptIds,
        CancellationToken cancellationToken);
    void AddRange(IEnumerable<DocumentDeptPermission> permissions);
    void RemoveRange(IEnumerable<DocumentDeptPermission> permissions);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IDocumentPermissionTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken);
}

public interface IDocumentPermissionTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
