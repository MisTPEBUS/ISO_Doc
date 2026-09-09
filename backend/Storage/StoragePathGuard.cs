using Microsoft.Extensions.Options;

namespace IsoDocument.Api.Storage;

public sealed class StoragePathGuard
{
    private static readonly char[] WindowsInvalidFileNameChars =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public StoragePathGuard(
        IOptions<StorageOptions> options,
        IHostEnvironment hostEnvironment)
        : this(ResolveConfiguredRoot(options.Value.RootPath, hostEnvironment.ContentRootPath))
    {
    }

    public StoragePathGuard(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Storage root path must be configured.", nameof(rootPath));
        }

        RootPath = Path.GetFullPath(rootPath);
    }

    public string RootPath { get; }

    public string ResolvePath(string objectKey)
    {
        var segments = ValidateRelativePath(objectKey);
        if (!string.Equals(segments[0], "store", StringComparison.Ordinal))
        {
            throw new InvalidStorageKeyException(
                objectKey,
                "A document object key must start with 'store/'.");
        }

        return ResolveSegments(objectKey, segments);
    }

    internal string ResolveInternalPath(string relativePath)
    {
        var segments = ValidateRelativePath(relativePath);
        return ResolveSegments(relativePath, segments);
    }

    private string ResolveSegments(string key, IReadOnlyList<string> segments)
    {
        var candidate = RootPath;
        foreach (var segment in segments)
        {
            candidate = Path.Combine(candidate, segment);
        }

        var fullPath = Path.GetFullPath(candidate);
        var rootPrefix = Path.EndsInDirectorySeparator(RootPath)
            ? RootPath
            : RootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, _pathComparison))
        {
            throw new InvalidStorageKeyException(key, "The object key escapes the storage root.");
        }

        return fullPath;
    }

    private static string[] ValidateRelativePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidStorageKeyException(key, "The object key cannot be empty.");
        }

        if (Path.IsPathRooted(key)
            || key.StartsWith("/", StringComparison.Ordinal)
            || key.StartsWith("\\", StringComparison.Ordinal)
            || (key.Length >= 2 && char.IsAsciiLetter(key[0]) && key[1] == ':'))
        {
            throw new InvalidStorageKeyException(key, "Absolute paths are not valid object keys.");
        }

        if (key.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidStorageKeyException(key, "Object keys must use '/' as their separator.");
        }

        var segments = key.Split('/');
        if (segments.Any(segment =>
                string.IsNullOrEmpty(segment)
                || segment is "." or ".."))
        {
            throw new InvalidStorageKeyException(key, "Object keys cannot contain empty or traversal segments.");
        }

        foreach (var segment in segments)
        {
            if (segment.Any(character =>
                    char.IsControl(character)
                    || WindowsInvalidFileNameChars.Contains(character))
                || segment.EndsWith(' ')
                || segment.EndsWith('.'))
            {
                throw new InvalidStorageKeyException(
                    key,
                    $"Object key segment '{segment}' contains invalid path characters.");
            }
        }

        return segments;
    }

    private static string ResolveConfiguredRoot(string configuredRoot, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException("Storage:RootPath must be configured.");
        }

        return Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.GetFullPath(configuredRoot, contentRoot);
    }
}

public sealed class InvalidStorageKeyException : ArgumentException
{
    public InvalidStorageKeyException(string? objectKey, string message)
        : base(message, nameof(objectKey))
    {
        ObjectKey = objectKey;
    }

    public string? ObjectKey { get; }
}
