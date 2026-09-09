using IsoDocument.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Security.Authorization;

public sealed class EfDocumentAccessStore(IsoDbContext dbContext) : IDocumentAccessStore
{
    public Task<Guid?> FindDocumentCompanyIdAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Documents
            .Where(document => document.Id == documentId)
            .Select(document => (Guid?)document.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> DeptHasAccessAsync(
        Guid documentId,
        Guid deptId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentDeptPermissions.AnyAsync(
            permission => permission.DocumentId == documentId
                && permission.DeptId == deptId,
            cancellationToken);
}
