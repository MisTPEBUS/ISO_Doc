namespace IsoDocument.Api.Features.AuditLogs;

public interface IAuditLogService
{
    Task WriteDocumentDeptPermissionsChangedAsync(
        Guid companyId,
        Guid documentId,
        IReadOnlyCollection<Guid> oldDeptIds,
        IReadOnlyCollection<Guid> newDeptIds,
        CancellationToken cancellationToken);
}
