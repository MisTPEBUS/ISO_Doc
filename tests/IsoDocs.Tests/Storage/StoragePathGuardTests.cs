using IsoDocument.Api.Storage;
using Xunit;

namespace IsoDocs.Tests.Storage;

public sealed class StoragePathGuardTests
{
    [Fact]
    public void ResolvePath_WhenObjectKeyIsValid_ReturnsPathUnderStorageRoot()
    {
        using var temp = new TempDirectory();
        var guard = new StoragePathGuard(temp.Path);
        const string objectKey = "store/ACME/HR-I-01/v1.0/main/file_document.pdf";

        var result = guard.ResolvePath(objectKey);

        var expected = System.IO.Path.Combine(
            temp.Path,
            "store",
            "ACME",
            "HR-I-01",
            "v1.0",
            "main",
            "file_document.pdf");
        Assert.Equal(System.IO.Path.GetFullPath(expected), result);
    }

    [Fact]
    public void ResolvePath_WhenObjectKeyContainsTraversal_ThrowsClearException()
    {
        using var temp = new TempDirectory();
        var guard = new StoragePathGuard(temp.Path);

        var exception = Assert.Throws<InvalidStorageKeyException>(() =>
            guard.ResolvePath("store/ACME/../secret.pdf"));

        Assert.Contains("traversal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePath_WhenObjectKeyIsAbsolute_ThrowsClearException()
    {
        using var temp = new TempDirectory();
        var guard = new StoragePathGuard(temp.Path);
        var absolutePath = OperatingSystem.IsWindows()
            ? @"C:\outside\secret.pdf"
            : "/outside/secret.pdf";

        var exception = Assert.Throws<InvalidStorageKeyException>(() =>
            guard.ResolvePath(absolutePath));

        Assert.Contains("Absolute", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
