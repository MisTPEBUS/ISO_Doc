namespace IsoDocument.Api.Storage;

public interface IDocumentStorage
{
    Task<StorageWriteResult> WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task MoveToTrashAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}

public sealed record StorageWriteResult(long FileSize, string Checksum);
