using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.AuditLogs;

public interface IAuditLogStore
{
    void Add(AuditLog auditLog);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
