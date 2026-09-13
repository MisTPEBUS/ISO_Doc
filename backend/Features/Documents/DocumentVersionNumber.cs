using System.Text.RegularExpressions;

namespace IsoDocument.Api.Features.Documents;

internal static partial class DocumentVersionNumber
{
    public static bool TryParse(
        string? value,
        out string normalizedVersion,
        out int versionMajor,
        out int versionMinor)
    {
        normalizedVersion = string.Empty;
        versionMajor = 0;
        versionMinor = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.Length > 20)
        {
            return false;
        }

        var match = VersionPattern().Match(trimmedValue);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out versionMajor)
            || (match.Groups[2].Success
                && !int.TryParse(match.Groups[2].Value, out versionMinor)))
        {
            versionMajor = 0;
            versionMinor = 0;
            return false;
        }

        normalizedVersion = $"{versionMajor}.{versionMinor}";
        return true;
    }

    [GeneratedRegex("^([1-9][0-9]*)(?:\\.(0|[1-9][0-9]*))?$")]
    private static partial Regex VersionPattern();
}
