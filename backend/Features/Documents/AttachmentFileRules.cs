using System.IO.Compression;

namespace IsoDocument.Api.Features.Documents;

public static class AttachmentFileRules
{
    private static readonly byte[] JpegMagic = [0xff, 0xd8, 0xff];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] OleMagic = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
    private static readonly byte[] ZipMagic = [0x50, 0x4b, 0x03, 0x04];
    private static readonly HashSet<string> AllowedExtensions = new(
        [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".odt", ".ods"],
        StringComparer.OrdinalIgnoreCase);

    public static bool HasAllowedExtension(string fileName) =>
        AllowedExtensions.Contains(GetExtension(fileName));

    public static string GetContentType(string fileName) => GetExtension(fileName) switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".odt" => "application/vnd.oasis.opendocument.text",
        ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        _ => "application/octet-stream"
    };

    public static async Task<bool> HasValidContentAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var expectedMagic = GetExtension(fileName) switch
        {
            ".jpg" or ".jpeg" => JpegMagic,
            ".png" => PngMagic,
            ".pdf" => PdfMagic,
            ".doc" or ".xls" => OleMagic,
            ".docx" or ".xlsx" or ".odt" or ".ods" => ZipMagic,
            _ => null
        };
        if (expectedMagic is null)
        {
            return false;
        }

        var header = new byte[expectedMagic.Length];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await content.ReadAsync(
                header.AsMemory(bytesRead, header.Length - bytesRead), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            bytesRead += read;
        }

        if (!header.AsSpan().SequenceEqual(expectedMagic))
        {
            return false;
        }

        var extension = GetExtension(fileName);
        if (extension is not (".docx" or ".xlsx" or ".odt" or ".ods"))
        {
            return true;
        }

        if (!content.CanSeek)
        {
            return false;
        }

        content.Position = 0;
        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            return extension switch
            {
                ".docx" => HasEntry(archive, "[Content_Types].xml")
                    && HasEntry(archive, "word/document.xml"),
                ".xlsx" => HasEntry(archive, "[Content_Types].xml")
                    && HasEntry(archive, "xl/workbook.xml"),
                ".odt" => await HasMimeTypeAsync(
                    archive, "application/vnd.oasis.opendocument.text", cancellationToken),
                ".ods" => await HasMimeTypeAsync(
                    archive, "application/vnd.oasis.opendocument.spreadsheet", cancellationToken),
                _ => false
            };
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool HasEntry(ZipArchive archive, string fullName) =>
        archive.Entries.Any(entry =>
            string.Equals(entry.FullName, fullName, StringComparison.OrdinalIgnoreCase));

    private static async Task<bool> HasMimeTypeAsync(
        ZipArchive archive,
        string expected,
        CancellationToken cancellationToken)
    {
        var entry = archive.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.FullName, "mimetype", StringComparison.Ordinal));
        if (entry is null || entry.Length > 100)
        {
            return false;
        }

        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, System.Text.Encoding.ASCII);
        var actual = await reader.ReadToEndAsync(cancellationToken);
        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static string GetExtension(string fileName)
    {
        var extensionStart = fileName.LastIndexOf('.');
        return extensionStart >= 0 ? fileName[extensionStart..].ToLowerInvariant() : string.Empty;
    }
}
