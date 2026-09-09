namespace IsoDocument.Api.Features.Documents;

public static class AttachmentFileRules
{
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

    private static string GetExtension(string fileName)
    {
        var extensionStart = fileName.LastIndexOf('.');
        return extensionStart >= 0 ? fileName[extensionStart..].ToLowerInvariant() : string.Empty;
    }
}
