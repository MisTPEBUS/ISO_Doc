using System.Security.Cryptography;
using System.Text;
using IsoDocument.Api.Storage;
using Xunit;

namespace IsoDocs.Tests.Storage;

public sealed class LocalFileStorageTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WriteAsync_WhenObjectAlreadyExists_DoesNotOverwriteExistingFile()
    {
        using var temp = new TempDirectory();
        var guard = new StoragePathGuard(temp.Path);
        var storage = new LocalFileStorage(guard, new FixedTimeProvider(TestTime));
        const string objectKey = "store/ACME/HR-I-01/v1.0/main/abc_document.pdf";
        var original = Encoding.UTF8.GetBytes("original content");

        var writeResult = await storage.WriteAsync(objectKey, new MemoryStream(original));
        await Assert.ThrowsAsync<IOException>(() =>
            storage.WriteAsync(objectKey, new MemoryStream(Encoding.UTF8.GetBytes("replacement"))));

        Assert.Equal(original.Length, writeResult.FileSize);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(),
            writeResult.Checksum);
        Assert.Equal(original, await File.ReadAllBytesAsync(guard.ResolvePath(objectKey)));
    }

    [Fact]
    public async Task MoveToTrashAsync_MovesFileUnderMonthAndPreservesObjectKeyPath()
    {
        using var temp = new TempDirectory();
        var guard = new StoragePathGuard(temp.Path);
        var storage = new LocalFileStorage(guard, new FixedTimeProvider(TestTime));
        const string objectKey = "store/ACME/HR-I-01/v1.0/att/01_abc_附件.pdf";
        var content = Encoding.UTF8.GetBytes("attachment");
        await storage.WriteAsync(objectKey, new MemoryStream(content));

        await storage.MoveToTrashAsync(objectKey);

        Assert.False(File.Exists(guard.ResolvePath(objectKey)));
        var trashPath = System.IO.Path.Combine(
            temp.Path,
            "trash",
            "202609",
            "store",
            "ACME",
            "HR-I-01",
            "v1.0",
            "att",
            "01_abc_附件.pdf");
        Assert.True(File.Exists(trashPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(trashPath));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
