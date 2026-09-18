using System.Text;
using System.Text.RegularExpressions;

namespace IsoDocument.Api.Storage;

public sealed partial class StorageKeyBuilder
{
    private const int MaximumBaseNameLength = 150;

    private static readonly HashSet<string> WindowsReservedNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    public string BuildMainKey(
        string companyCode,
        string documentNo,
        string version,
        Guid fileId,
        string originalFileName)
    {
        ValidateIdentifier(companyCode, nameof(companyCode));
        ValidateIdentifier(documentNo, nameof(documentNo));
        ValidateVersion(version);

        return $"store/{companyCode}/{documentNo}/main/v{version}/{fileId:N}_{ToSafeName(originalFileName)}";
    }

    public string BuildAttachmentKey(
        string companyCode,
        string documentNo,
        string? attachmentNo,
        Guid attachmentId,
        string version,
        Guid fileId,
        string originalFileName)
    {
        ValidateIdentifier(companyCode, nameof(companyCode));
        ValidateIdentifier(documentNo, nameof(documentNo));
        ValidateVersion(version);

        string attachmentSegment;
        if (string.IsNullOrWhiteSpace(attachmentNo))
        {
            attachmentSegment = $"_{attachmentId:N}";
        }
        else
        {
            ValidateIdentifier(attachmentNo, nameof(attachmentNo));
            attachmentSegment = attachmentNo;
        }

        return $"store/{companyCode}/{documentNo}/att/{attachmentSegment}/v{version}/{fileId:N}_{ToSafeName(originalFileName)}";
    }

    public string ToSafeName(string originalFileName)
    {
        ArgumentNullException.ThrowIfNull(originalFileName);

        var cleaned = new StringBuilder(originalFileName.Length);
        foreach (var character in originalFileName)
        {
            if (character is '/' or '\\' || char.IsControl(character))
            {
                continue;
            }

            cleaned.Append(character is '<' or '>' or ':' or '"' or '|' or '?' or '*'
                ? '_'
                : character);
        }

        var safeName = cleaned.ToString().TrimEnd(' ', '.');
        if (safeName.Length == 0)
        {
            return "file";
        }

        var extensionStart = safeName.LastIndexOf('.');
        var hasExtension = extensionStart > 0 && extensionStart < safeName.Length - 1;
        var baseName = hasExtension ? safeName[..extensionStart] : safeName;
        var extension = hasExtension ? safeName[extensionStart..].ToLowerInvariant() : string.Empty;

        baseName = baseName.TrimEnd(' ', '.');
        if (baseName.Length == 0)
        {
            baseName = "file";
        }

        if (baseName.Length > MaximumBaseNameLength)
        {
            baseName = baseName[..MaximumBaseNameLength];
        }

        var deviceName = baseName.Split('.', 2)[0];
        if (WindowsReservedNames.Contains(deviceName))
        {
            baseName = $"_{baseName}";
        }

        return baseName + extension;
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Storage key identifiers cannot be empty.", parameterName);
        }

        if (value is "." or ".."
            || value.EndsWith(' ')
            || value.EndsWith('.')
            || value.Any(character =>
                char.IsControl(character)
                || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid storage key identifier.",
                parameterName);
        }
    }

    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || !VersionPattern().IsMatch(version))
        {
            throw new ArgumentException(
                "Version must use the non-negative 'major.minor' format.",
                nameof(version));
        }
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
