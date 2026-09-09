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

    public async Task<IReadOnlyList<DocumentDeptPermission>> ListPermissionsAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.DocumentDeptPermissions
            .Where(permission => permission.DocumentId == documentId)
            .OrderBy(permission => permission.DeptId)
            .ToListAsync(cancellationToken);

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
