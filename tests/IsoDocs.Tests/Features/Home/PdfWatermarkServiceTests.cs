using IsoDocument.Api.Features.Home;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace IsoDocs.Tests.Features.Home;

public sealed class PdfWatermarkServiceTests
{
    static PdfWatermarkServiceTests()
    {
        // 這個測試不透過 WebApplicationFactory<Program> 啟動流程，
        // 要自己確保 GlobalFontSettings.FontResolver 已設定，否則字型無從解析。
        GlobalFontSettings.FontResolver = new WatermarkFontResolver();
    }

    [Fact]
    public async Task ApplyWatermarkAsync_WithGuidDocumentId_ProducesValidSamePageCountPdf()
    {
        var service = new PdfWatermarkService();
        var sourceBytes = CreatePdfBytes(pageCount: 2);
        var content = new PdfWatermarkContent(
            CompanyCode: "COA",
            DocumentId: Guid.NewGuid().ToString());

        await using var result = await service.ApplyWatermarkAsync(
            new MemoryStream(sourceBytes), content, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result.Position);
        using var watermarked = PdfReader.Open(result, PdfDocumentOpenMode.Import);
        Assert.Equal(2, watermarked.PageCount);
    }

    [Fact]
    public async Task ApplyWatermarkAsync_DoesNotMutateSourceBytes()
    {
        var service = new PdfWatermarkService();
        var sourceBytes = CreatePdfBytes(pageCount: 1);
        var sourceCopy = (byte[])sourceBytes.Clone();
        var content = new PdfWatermarkContent("COA", "ISO-001");

        await using var result = await service.ApplyWatermarkAsync(
            new MemoryStream(sourceBytes), content, CancellationToken.None);

        Assert.Equal(sourceCopy, sourceBytes);
    }

    private static byte[] CreatePdfBytes(int pageCount)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            document.AddPage();
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
