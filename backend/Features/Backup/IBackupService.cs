using IsoDocument.Api.Common;

namespace IsoDocument.Api.Features.Backup;

public sealed record CompanyBackup(
    Guid CompanyId,
    string FileName,
    IReadOnlyList<BackupArchiveEntry> Entries);

public sealed record BackupArchiveEntry(string ObjectKey, string EntryName);

public interface IBackupService
{
    Task<Result<CompanyBackup>> PrepareAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task WriteAsync(
        CompanyBackup backup,
        Stream destination,
        CancellationToken cancellationToken);
}
