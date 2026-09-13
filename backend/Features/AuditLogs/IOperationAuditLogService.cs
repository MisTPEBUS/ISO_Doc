namespace IsoDocument.Api.Features.AuditLogs;

public sealed record AuditLogWriteRequest(
    Guid? CompanyId,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    object? Detail = null);

public interface IOperationAuditLogService
{
    Task WriteAsync(
        AuditLogWriteRequest request,
        CancellationToken cancellationToken);
}

public static class AuditActions
{
    public const string CreateDept = "CREATE_DEPT";
    public const string UpdateDept = "UPDATE_DEPT";
    public const string DeleteDept = "DELETE_DEPT";
    public const string CreateUser = "CREATE_USER";
    public const string BatchCreateUsers = "BATCH_CREATE_USERS";
    public const string UpdateUser = "UPDATE_USER";
    public const string DeleteUser = "DELETE_USER";
    public const string ResetUserPassword = "RESET_USER_PASSWORD";
    public const string CreateDocument = "CREATE_DOCUMENT";
    public const string UpdateDocument = "UPDATE_DOCUMENT";
    public const string DeleteDocument = "DELETE_DOCUMENT";
    public const string PublishDocumentVersion = "PUBLISH_DOCUMENT_VERSION";
    public const string DeleteDocumentVersion = "DELETE_DOCUMENT_VERSION";
    public const string UploadAttachment = "UPLOAD_ATTACHMENT";
    public const string CreateAttachmentMetadata = "CREATE_ATTACHMENT_METADATA";
    public const string DeleteAttachment = "DELETE_ATTACHMENT";
    public const string UpdateDocumentDeptPermissions = "UPDATE_DOCUMENT_DEPT_PERMISSIONS";
    public const string DownloadDocument = "DOWNLOAD_DOCUMENT";
    public const string DownloadAttachment = "DOWNLOAD_ATTACHMENT";
    public const string BackupCompanyDocuments = "BACKUP_COMPANY_DOCUMENTS";
}

public static class AuditResourceTypes
{
    public const string Dept = "Dept";
    public const string User = "User";
    public const string Document = "Document";
    public const string DocumentVersion = "DocumentVersion";
    public const string Attachment = "Attachment";
    public const string AttachmentVersion = "AttachmentVersion";
    public const string Company = "Company";
}
