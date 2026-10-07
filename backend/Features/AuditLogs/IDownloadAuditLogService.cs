namespace IsoDocument.Api.Features.AuditLogs;

public interface IDownloadAuditLogService
{
    Task WriteDocumentDownloadedAsync(
        Guid companyId, Guid documentId, Guid versionId, Guid downloadId,
        CancellationToken cancellationToken);
    Task WriteAttachmentDownloadedAsync(
        Guid companyId, Guid attachmentId, CancellationToken cancellationToken);
}
