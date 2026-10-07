namespace IsoDocument.Api.Features.Documents;

/// <summary>
/// ISO管理程序檔案（僅 PDF）的內容驗證；表單及附件白名單另見 <see cref="AttachmentFileRules"/>。
/// </summary>
internal static class DocumentFileRules
{
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    public static async Task<bool> HasPdfMagicBytesAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new byte[PdfMagicBytes.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead), cancellationToken);
            if (bytesRead == 0)
            {
                return false;
            }

            totalRead += bytesRead;
        }

        return buffer.AsSpan().SequenceEqual(PdfMagicBytes);
    }
}
