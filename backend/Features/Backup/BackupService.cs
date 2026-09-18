using System.Globalization;
using System.IO.Compression;
using IsoDocument.Api.Common;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Storage;

namespace IsoDocument.Api.Features.Backup;

public sealed class BackupService(
    IBackupStore backupStore,
    IDocumentStorage documentStorage,
    IBackupAuditLogService auditLogService,
    StorageKeyBuilder storageKeyBuilder,
    TimeProvider timeProvider) : IBackupService
{
    private const int CopyBufferSize = 81920;

    public async Task<Result<CompanyBackup>> PrepareAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await backupStore.FindCompanyAsync(companyId, cancellationToken);
        if (company is null)
        {
            return Result<CompanyBackup>.NotFound("找不到指定的公司。");
        }

        var sourceFiles = await backupStore.ListSourceFilesAsync(
            companyId, cancellationToken);
        var entries = sourceFiles
            .Where(file => file.VersionStatus == "PUBLISHED"
                && !string.IsNullOrWhiteSpace(file.FileKey))
            .OrderBy(file => file.DocumentNo, StringComparer.Ordinal)
            .ThenBy(file => file.Version, StringComparer.Ordinal)
            .ThenBy(file => file.AttachmentId is not null)
            .ThenBy(file => file.AttachmentNo, StringComparer.Ordinal)
            .Select(ToArchiveEntry)
            .ToArray();
        var date = timeProvider.GetUtcNow().ToString(
            "yyyyMMdd", CultureInfo.InvariantCulture);
        var fileName = storageKeyBuilder.ToSafeName($"{company.Name}_backup_{date}.zip");

        return Result<CompanyBackup>.Success(new(company.Id, fileName, entries));
    }

    public async Task WriteAsync(
        CompanyBackup backup,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(destination);

        await using var zipOutput = new ZipArchiveOutputStream(destination);
        await using (var archive = new ZipArchive(
            zipOutput, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var sourceFile in backup.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var source = await documentStorage.OpenReadAsync(
                    sourceFile.ObjectKey, cancellationToken);
                var entry = archive.CreateEntry(
                    sourceFile.EntryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await source.CopyToAsync(
                    entryStream, CopyBufferSize, cancellationToken);
            }
        }

        await auditLogService.WriteCompanyBackupCreatedAsync(
            backup.CompanyId, cancellationToken);
    }

    private BackupArchiveEntry ToArchiveEntry(BackupSourceFile source)
    {
        var documentNo = storageKeyBuilder.ToSafeName(source.DocumentNo);
        var version = storageKeyBuilder.ToSafeName(source.Version);
        var originalFileName = storageKeyBuilder.ToSafeName(
            source.OriginalFileName ?? "file");
        var entryName = source.AttachmentId is null
            ? $"{documentNo}/v{version}/main/{originalFileName}"
            : $"{documentNo}/v{version}/attachments/" +
                $"{AttachmentSegment(source)}_{originalFileName}";
        return new BackupArchiveEntry(source.FileKey!, entryName);
    }

    private string AttachmentSegment(BackupSourceFile source) =>
        string.IsNullOrWhiteSpace(source.AttachmentNo)
            ? $"_{source.AttachmentId:N}"
            : storageKeyBuilder.ToSafeName(source.AttachmentNo);

    private sealed class ZipArchiveOutputStream(Stream destination) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => destination.CanSeek;
        public override bool CanWrite => true;
        public override long Length => destination.Length;
        public override long Position
        {
            get => destination.Position;
            set => destination.Position = value;
        }

        public override void Flush() =>
            destination.FlushAsync().GetAwaiter().GetResult();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            destination.FlushAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) =>
            destination.WriteAsync(buffer.AsMemory(offset, count))
                .AsTask()
                .GetAwaiter()
                .GetResult();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            destination.WriteAsync(buffer, cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            destination.Seek(offset, origin);

        public override void SetLength(long value) => destination.SetLength(value);
    }
}
