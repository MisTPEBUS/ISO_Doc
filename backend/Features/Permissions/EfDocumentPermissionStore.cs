using System.Data;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IsoDocument.Api.Features.Permissions;

public sealed class EfDocumentPermissionStore(IsoDbContext dbContext)
    : IDocumentPermissionStore
{
    public Task<Document?> FindDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Documents.SingleOrDefaultAsync(
            document => document.Id == documentId,
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Document>> FindDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken) =>
        (await dbContext.Documents
            .Where(document => documentIds.Contains(document.Id))
            .ToListAsync(cancellationToken))
        .ToDictionary(document => document.Id);

    public async Task<IReadOnlyList<DocumentDeptPermission>> ListPermissionsAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.DocumentDeptPermissions
            .Where(permission => permission.DocumentId == documentId)
            .OrderBy(permission => permission.DeptId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentDeptPermission>>> ListPermissionsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        var permissions = await dbContext.DocumentDeptPermissions
            .Where(permission => documentIds.Contains(permission.DocumentId))
            .ToListAsync(cancellationToken);
        return permissions
            .GroupBy(permission => permission.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DocumentDeptPermission>)group.ToArray());
    }

    public async Task<IReadOnlySet<Guid>> FindCompanyDeptIdsAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> deptIds,
        CancellationToken cancellationToken) =>
        (await dbContext.Depts
            .Where(dept => dept.CompanyId == companyId && deptIds.Contains(dept.Id))
            .Select(dept => dept.Id)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public void AddRange(IEnumerable<DocumentDeptPermission> permissions) =>
        dbContext.DocumentDeptPermissions.AddRange(permissions);

    public void RemoveRange(IEnumerable<DocumentDeptPermission> permissions) =>
        dbContext.DocumentDeptPermissions.RemoveRange(permissions);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IDocumentPermissionTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken) =>
        new EfDocumentPermissionTransaction(await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken));

    private sealed class EfDocumentPermissionTransaction(IDbContextTransaction transaction)
        : IDocumentPermissionTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
