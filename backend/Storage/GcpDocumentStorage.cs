using System.IO.Pipelines;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace IsoDocument.Api.Storage;

public sealed class GcpDocumentStorage(
    StorageClient client,
    IOptions<StorageOptions> storageOptions,
    IOptions<GcpStorageOptions> options,
    TimeProvider timeProvider,
    ILogger<GcpDocumentStorage> logger) : IDocumentStorage
{
    private readonly string _bucket = storageOptions.Value.BucketName;
    private readonly string _prefix = options.Value.ObjectPrefix.Trim('/');

    public async Task<StorageWriteResult> WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(objectKey);
        ArgumentNullException.ThrowIfNull(content);

        await using var tracking = new HashingReadStream(content);
        try
        {
            await client.UploadObjectAsync(
                _bucket,
                objectKey,
                "application/octet-stream",
                tracking,
                new UploadObjectOptions { IfGenerationMatch = 0 },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not GoogleApiException
            { HttpStatusCode: HttpStatusCode.PreconditionFailed })
        {
            await ReconcileFailedUploadAsync(objectKey);
            if (IsTransientUploadFailure(exception, cancellationToken))
            {
                throw new GcpStorageUnavailableException(exception);
            }

            throw;
        }
        return new StorageWriteResult(tracking.BytesRead, tracking.Sha256);
    }

    private async Task ReconcileFailedUploadAsync(string objectKey)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await client.GetObjectAsync(_bucket, objectKey, cancellationToken: timeout.Token);
            await MoveToTrashAsync(objectKey, timeout.Token);
            logger.LogWarning(
                "Upload failed after object {ObjectKey} was created; moved the uncommitted object to trash.",
                objectKey);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // The upload did not create a visible object.
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Could not reconcile failed GCP upload for {ObjectKey}; manual reconciliation is required.",
                objectKey);
        }
    }

    private static bool IsTransientUploadFailure(Exception exception, CancellationToken requestToken) =>
        exception switch
        {
            GoogleApiException google => google.HttpStatusCode is
                HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                or >= HttpStatusCode.InternalServerError,
            HttpRequestException => true,
            TimeoutException => true,
            OperationCanceledException => !requestToken.IsCancellationRequested,
            _ => false
        };

    public async Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(objectKey);
        await client.GetObjectAsync(_bucket, objectKey, cancellationToken: cancellationToken);

        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: 1024 * 1024,
            resumeWriterThreshold: 512 * 1024));
        _ = DownloadIntoPipeAsync(objectKey, pipe, cancellationToken);
        return pipe.Reader.AsStream();
    }

    public async Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(objectKey);
        try
        {
            await client.GetObjectAsync(_bucket, objectKey, cancellationToken: cancellationToken);
            return true;
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task MoveToTrashAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(objectKey);
        var month = timeProvider.GetUtcNow().ToString(
            "yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
        var trashKey = $"trash/{month}/{objectKey}";
        return client.MoveObjectAsync(
            _bucket,
            objectKey,
            trashKey,
            new MoveObjectOptions { IfGenerationMatch = 0 },
            cancellationToken);
    }

    private async Task DownloadIntoPipeAsync(
        string objectKey,
        Pipe pipe,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DownloadObjectAsync(
                _bucket,
                objectKey,
                pipe.Writer.AsStream(leaveOpen: true),
                cancellationToken: cancellationToken);
            await pipe.Writer.CompleteAsync();
        }
        catch (Exception exception)
        {
            await pipe.Writer.CompleteAsync(exception);
        }
    }

    private void ValidateKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(_bucket)
            || string.IsNullOrWhiteSpace(_prefix))
        {
            throw new InvalidOperationException("Storage:BucketName and Gcp:ObjectPrefix must be configured.");
        }

        if (string.IsNullOrWhiteSpace(objectKey)
            || objectKey.Length > 1024
            || Encoding.UTF8.GetByteCount(objectKey) > 1024
            || !objectKey.StartsWith(_prefix + "/", StringComparison.Ordinal)
            || objectKey.Contains('\\')
            || objectKey.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidStorageKeyException(objectKey, "Invalid GCP storage object key.");
        }
    }

    private sealed class HashingReadStream(Stream source) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private string? _sha256;

        public long BytesRead { get; private set; }
        public string Sha256 => _sha256 ??= Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = source.Read(buffer, offset, count);
            Append(buffer.AsSpan(offset, read));
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            Append(buffer.Span[..read]);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await source.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            Append(buffer.AsSpan(offset, read));
            return read;
        }

        private void Append(ReadOnlySpan<byte> data)
        {
            _hash.AppendData(data);
            BytesRead += data.Length;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hash.Dispose();
            base.Dispose(disposing);
        }
    }
}
