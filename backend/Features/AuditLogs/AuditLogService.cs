using System.Text.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Security;

namespace IsoDocument.Api.Features.AuditLogs;

public sealed class AuditLogService(
    IAuditLogStore auditLogStore,
    ICurrentUser currentUser,
    TimeProvider timeProvider) :
    IAuditLogService,
    IDownloadAuditLogService,
    IBackupAuditLogService,
    IOperationAuditLogService
{
    public Task WriteDocumentDeptPermissionsChangedAsync(
        Guid companyId,
        Guid documentId,
        IReadOnlyCollection<Guid> oldDeptIds,
        IReadOnlyCollection<Guid> newDeptIds,
        CancellationToken cancellationToken)
    {
        return WriteAsync(
            new AuditLogWriteRequest(
                companyId,
                AuditActions.UpdateDocumentDeptPermissions,
                AuditResourceTypes.Document,
                documentId,
                new
                {
                    old_value = oldDeptIds.Order().ToArray(),
                    new_value = newDeptIds.Order().ToArray()
                }),
            cancellationToken);
    }

    public Task WriteDocumentDownloadedAsync(
        Guid companyId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        WriteAsync(
            new AuditLogWriteRequest(
                companyId,
                AuditActions.DownloadDocument,
                AuditResourceTypes.DocumentVersion,
                versionId),
            cancellationToken);

    public Task WriteAttachmentDownloadedAsync(
        Guid companyId,
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        WriteAsync(
            new AuditLogWriteRequest(
                companyId,
                AuditActions.DownloadAttachment,
                AuditResourceTypes.Attachment,
                attachmentId),
            cancellationToken);

    public Task WriteCompanyBackupCreatedAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        WriteAsync(
            new AuditLogWriteRequest(
                companyId,
                AuditActions.BackupCompanyDocuments,
                AuditResourceTypes.Company,
                companyId),
            cancellationToken);

    public async Task WriteAsync(
        AuditLogWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId
            || string.IsNullOrWhiteSpace(currentUser.Empno))
        {
            throw new InvalidOperationException(
                "An authenticated user id and employee number are required for audit logging.");
        }

        auditLogStore.Add(new AuditLog
        {
            CompanyId = request.CompanyId,
            UserId = userId,
            Empno = currentUser.Empno,
            Action = request.Action,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Detail = request.Detail is null
                ? null
                : JsonSerializer.Serialize(request.Detail),
            CreatedAt = timeProvider.GetUtcNow()
        });
        await auditLogStore.SaveChangesAsync(cancellationToken);
    }
}
