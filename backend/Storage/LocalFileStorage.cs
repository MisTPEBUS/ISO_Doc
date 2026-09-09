using System.Security.Cryptography;

namespace IsoDocument.Api.Storage;

public sealed class LocalFileStorage(
    StoragePathGuard pathGuard,
    TimeProvider timeProvider) : IDocumentStorage
{
    private const int BufferSize = 81920;

    public async Task<StorageWriteResult> WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var destinationPath = pathGuard.ResolvePath(objectKey);
        var stagingKey = $"staging/{Guid.NewGuid():N}.tmp";
        var stagingPath = pathGuard.ResolveInternalPath(stagingKey);
        var stagingCreated = false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            long fileSize = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var destination = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                stagingCreated = true;
                var buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    hash.AppendData(buffer, 0, bytesRead);
                    fileSize += bytesRead;
                }

                await destination.FlushAsync(cancellationToken);
            }

            File.Move(stagingPath, destinationPath, overwrite: false);
            stagingCreated = false;

            return new StorageWriteResult(
                fileSize,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            if (stagingCreated && File.Exists(stagingPath))
            {
                TryMoveFailedStagingFileToTrash(stagingPath);
            }

            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = pathGuard.ResolvePath(objectKey);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(pathGuard.ResolvePath(objectKey)));
    }

    public Task MoveToTrashAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = pathGuard.ResolvePath(objectKey);
        var trashKey = $"trash/{GetTrashMonth()}/{objectKey}";
        var trashPath = pathGuard.ResolveInternalPath(trashKey);
        Directory.CreateDirectory(Path.GetDirectoryName(trashPath)!);
        File.Move(sourcePath, trashPath, overwrite: false);
        return Task.CompletedTask;
    }

    private void TryMoveFailedStagingFileToTrash(string stagingPath)
    {
        try
        {
            var fileName = Path.GetFileName(stagingPath);
            var trashPath = pathGuard.ResolveInternalPath(
                $"trash/{GetTrashMonth()}/staging/{fileName}");
            Directory.CreateDirectory(Path.GetDirectoryName(trashPath)!);
            File.Move(stagingPath, trashPath, overwrite: false);
        }
        catch
        {
            // Preserve the original write exception. A later trash GC can handle the staging file.
        }
    }

    private string GetTrashMonth() =>
        timeProvider.GetUtcNow().ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
}
