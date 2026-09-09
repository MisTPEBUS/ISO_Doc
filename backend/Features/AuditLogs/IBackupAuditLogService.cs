namespace IsoDocument.Api.Features.AuditLogs;

public interface IBackupAuditLogService
{
    Task WriteCompanyBackupCreatedAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
