namespace IsoDocument.Api.Features.Backup;

public sealed record BackupCompany(Guid Id, string Name);

public sealed record BackupSourceFile(
    string DocumentNo,
    string Version,
    string VersionStatus,
    string? FileKey,
    string? OriginalFileName,
    Guid? AttachmentId,
    string? AttachmentNo);

public interface IBackupStore
{
    Task<BackupCompany?> FindCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupSourceFile>> ListSourceFilesAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
