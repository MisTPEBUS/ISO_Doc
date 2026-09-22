using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace IsoDocument.Api.Features.Home;

/// <summary>
/// 用 PDFsharp 在既有 PDF 的每一頁疊加浮水印文字圖層。
/// </summary>
public sealed class PdfWatermarkService : IPdfWatermarkService
{
    private const double CenterFontSize = 64;
    private const double SideFontSize = 24;
    private const double MarginPoints = 18;
    private const double SideTextBottomMargin = 40;
    private const double CenterRotationDegrees = 45;

    // 中央浮水印使用 Append，確保掃描 PDF、白底 PDF 也能顯示。
    // Alpha 45 ≈ 17.6% 不透明度；搭配 PDF 1.4+ 的透明度支援，
    // 浮水印可見，但底下黑色正文仍可清楚閱讀。
    private static readonly XColor CenterWatermarkColor =
        XColor.FromArgb(120, 120, 120, 120);

    private static readonly XColor SideWatermarkColor =
        XColor.FromArgb(120, 120, 120, 120);

    public Task<Stream> ApplyWatermarkAsync(
        Stream source,
        PdfWatermarkContent content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);

        // PDF transparency（透明度）需要 PDF 1.4 以上。
        // 若來源是舊版 PDF 而沒有升級版本，部分 Viewer 可能把 Alpha 當成不透明處理，
        // 導致灰色浮水印直接蓋住正文。
        if (document.Version < 14)
        {
            document.Version = 14;
        }

        var fontOptions = new XPdfFontOptions(
            PdfFontEncoding.Unicode,
            PdfFontEmbedding.TryComputeSubset);

        var centerFont = new XFont(
            WatermarkFontResolver.LatinFamilyName,
            CenterFontSize,
            XFontStyleEx.Regular,
            fontOptions);

        var sideFont = new XFont(
            WatermarkFontResolver.ChineseFamilyName,
            SideFontSize,
            XFontStyleEx.Bold,
            fontOptions);

        var centerBrush = new XSolidBrush(CenterWatermarkColor);
        var sideBrush = new XSolidBrush(SideWatermarkColor);

        var sideText = $"{content.CompanyCode} {content.DocumentId}";

        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var width = page.Width.Point;
            var height = page.Height.Point;

            // Append：浮水印畫在既有 PDF 內容上方。
            // 透明度由 XColor Alpha 控制。
            using var gfx = XGraphics.FromPdfPage(
                page,
                XGraphicsPdfPageOptions.Append);

            // =========================================================
            // 正中央公司代碼浮水印
            // =========================================================
            gfx.Save();

            gfx.TranslateTransform(
                width / 2,
                height / 2);

            gfx.RotateTransform(CenterRotationDegrees);

            gfx.DrawString(
                content.CompanyCode,
                centerFont,
                centerBrush,
                new XPoint(0, 0),
                XStringFormats.Center);

            gfx.Restore();

            // =========================================================
            // 左邊界直式「公司代碼 + 文件 id」
            // =========================================================
            gfx.Save();

            gfx.TranslateTransform(
                MarginPoints,
                height - SideTextBottomMargin);

            gfx.RotateTransform(90);

            gfx.DrawString(
                sideText,
                sideFont,
                sideBrush,
                new XPoint(0, 0),
                XStringFormats.BottomRight);

            gfx.Restore();
        }

        var output = new MemoryStream();

        document.Save(
            output,
            closeStream: false);

        output.Position = 0;

        return Task.FromResult<Stream>(output);
    }
}
