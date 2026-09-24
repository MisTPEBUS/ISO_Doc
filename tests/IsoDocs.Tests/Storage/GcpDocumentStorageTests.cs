using System.Security.Cryptography;
using System.Net;
using Google;
using Google.Cloud.Storage.V1;
using Google.Apis.Storage.v1.Data;
using IsoDocument.Api.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IsoDocs.Tests.Storage;

public sealed class GcpDocumentStorageTests
{
    private const string Key = "documents/iso/company/category/DOC-1/main/v1.0/file_document.pdf";

    [Fact]
    public async Task WriteAsync_UsesCreateOnlyPreconditionAndReturnsSha256()
    {
        var client = new FakeStorageClient();
        var storage = CreateStorage(client);
        var content = "test document"u8.ToArray();

        var result = await storage.WriteAsync(Key, new MemoryStream(content));

        Assert.Equal(0, client.UploadIfGenerationMatch);
        Assert.Equal(content.Length, result.FileSize);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            result.Checksum);
        Assert.Equal(content, client.Objects[Key]);
    }

    [Fact]
    public async Task MoveToTrashAsync_UsesMonthAndNoOverwritePrecondition()
    {
        var client = new FakeStorageClient();
        var storage = CreateStorage(client);

        await storage.MoveToTrashAsync(Key);

        Assert.Equal(Key, client.MovedSource);
        Assert.Equal("trash/202609/" + Key, client.MovedDestination);
        Assert.Equal(0, client.MoveIfGenerationMatch);
    }

    [Fact]
    public async Task OpenReadAsync_StreamsOriginalBytes()
    {
        var client = new FakeStorageClient();
        client.Objects[Key] = "streamed content"u8.ToArray();
        var storage = CreateStorage(client);

        await using var stream = await storage.OpenReadAsync(Key);
        using var received = new MemoryStream();
        await stream.CopyToAsync(received);

        Assert.Equal(client.Objects[Key], received.ToArray());
    }

    [Fact]
    public async Task WriteAsync_RejectsObjectNameOverGcsByteLimit()
    {
        var client = new FakeStorageClient();
        var storage = CreateStorage(client);
        var key = "documents/iso/" + new string('文', 400);

        await Assert.ThrowsAsync<InvalidStorageKeyException>(() =>
            storage.WriteAsync(key, new MemoryStream("content"u8.ToArray())));
        Assert.Empty(client.Objects);
    }

    [Fact]
    public async Task WriteAsync_WhenUploadFailsBeforeObjectExists_LeavesNoObject()
    {
        var client = new FakeStorageClient { UploadException = new TimeoutException("timeout") };
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<GcpStorageUnavailableException>(() =>
            storage.WriteAsync(Key, new MemoryStream("content"u8.ToArray())));

        Assert.False(client.Objects.ContainsKey(Key));
        Assert.Null(client.MovedSource);
    }

    [Fact]
    public async Task WriteAsync_WhenResponseFailsAfterObjectExists_MovesObjectToTrash()
    {
        var client = new FakeStorageClient
        {
            UploadException = new TimeoutException("response lost"),
            ThrowUploadAfterWrite = true
        };
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<GcpStorageUnavailableException>(() =>
            storage.WriteAsync(Key, new MemoryStream("content"u8.ToArray())));

        Assert.Equal(Key, client.MovedSource);
        Assert.False(client.Objects.ContainsKey(Key));
    }

    [Fact]
    public async Task WriteAsync_WhenReconciliationMoveFails_ReportsUploadFailureAndLeavesObjectForReview()
    {
        var client = new FakeStorageClient
        {
            UploadException = new TimeoutException("response lost"),
            ThrowUploadAfterWrite = true,
            MoveException = new IOException("trash unavailable")
        };
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<GcpStorageUnavailableException>(() =>
            storage.WriteAsync(Key, new MemoryStream("content"u8.ToArray())));

        Assert.Equal(Key, client.MovedSource);
        Assert.True(client.Objects.ContainsKey(Key));
    }

    [Fact]
    public async Task WriteAsync_WhenObjectAlreadyExists_DoesNotMoveExistingObject()
    {
        var client = new FakeStorageClient
        {
            UploadException = new GoogleApiException("storage", "already exists")
            { HttpStatusCode = HttpStatusCode.PreconditionFailed }
        };
        client.Objects[Key] = "existing"u8.ToArray();
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<GoogleApiException>(() =>
            storage.WriteAsync(Key, new MemoryStream("new"u8.ToArray())));

        Assert.Null(client.MovedSource);
        Assert.Equal("existing"u8.ToArray(), client.Objects[Key]);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task WriteAsync_WhenGcpReturnsTransientStatus_ThrowsUnavailable(
        HttpStatusCode status)
    {
        var client = new FakeStorageClient
        {
            UploadException = new GoogleApiException("storage", "temporary failure")
            { HttpStatusCode = status }
        };
        var storage = CreateStorage(client);

        await Assert.ThrowsAsync<GcpStorageUnavailableException>(() =>
            storage.WriteAsync(Key, new MemoryStream("content"u8.ToArray())));
    }

    private static GcpDocumentStorage CreateStorage(FakeStorageClient client) => new(
        client,
        Options.Create(new StorageOptions
        {
            Provider = "GoogleCloud",
            BucketName = "test-bucket"
        }),
        Options.Create(new GcpStorageOptions
        {
            ObjectPrefix = "documents/iso"
        }),
        new FixedTimeProvider(),
        NullLogger<GcpDocumentStorage>.Instance);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeStorageClient : StorageClient
    {
        public Dictionary<string, byte[]> Objects { get; } = new();
        public long? UploadIfGenerationMatch { get; private set; }
        public long? MoveIfGenerationMatch { get; private set; }
        public string? MovedSource { get; private set; }
        public string? MovedDestination { get; private set; }
        public Exception? UploadException { get; init; }
        public bool ThrowUploadAfterWrite { get; init; }
        public Exception? MoveException { get; init; }

        public override async Task<Google.Apis.Storage.v1.Data.Object> UploadObjectAsync(
            string bucket, string objectName, string contentType, Stream source,
            UploadObjectOptions? options = null, CancellationToken cancellationToken = default,
            IProgress<Google.Apis.Upload.IUploadProgress>? progress = null)
        {
            UploadIfGenerationMatch = options?.IfGenerationMatch;
            if (!ThrowUploadAfterWrite && UploadException is not null) throw UploadException;
            using var output = new MemoryStream();
            await source.CopyToAsync(output, cancellationToken);
            Objects[objectName] = output.ToArray();
            if (UploadException is not null) throw UploadException;
            return new Google.Apis.Storage.v1.Data.Object();
        }

        public override Task<Google.Apis.Storage.v1.Data.Object> GetObjectAsync(
            string bucket, string objectName, GetObjectOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Objects.ContainsKey(objectName)
                ? Task.FromResult(new Google.Apis.Storage.v1.Data.Object())
                : Task.FromException<Google.Apis.Storage.v1.Data.Object>(
                    new GoogleApiException("storage", "not found")
                    { HttpStatusCode = HttpStatusCode.NotFound });

        public override async Task<Google.Apis.Storage.v1.Data.Object> DownloadObjectAsync(
            string bucket, string objectName, Stream destination,
            DownloadObjectOptions? options = null,
            CancellationToken cancellationToken = default,
            IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
        {
            await destination.WriteAsync(Objects[objectName], cancellationToken);
            return new Google.Apis.Storage.v1.Data.Object();
        }

        public override Task<Google.Apis.Storage.v1.Data.Object> MoveObjectAsync(
            string sourceBucket, string sourceObjectName, string destinationObjectName,
            MoveObjectOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            MovedSource = sourceObjectName;
            MovedDestination = destinationObjectName;
            MoveIfGenerationMatch = options?.IfGenerationMatch;
            if (MoveException is not null)
                return Task.FromException<Google.Apis.Storage.v1.Data.Object>(MoveException);
            Objects.Remove(sourceObjectName);
            return Task.FromResult(new Google.Apis.Storage.v1.Data.Object());
        }
    }
}
