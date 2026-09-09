using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.AuditLogs;

public sealed class EfAuditLogStore(IsoDbContext dbContext) : IAuditLogStore
{
    public void Add(AuditLog auditLog) => dbContext.AuditLogs.Add(auditLog);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
