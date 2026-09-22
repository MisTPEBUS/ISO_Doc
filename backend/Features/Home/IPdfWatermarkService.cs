namespace IsoDocument.Api.Features.Home;

/// <summary>
/// 主文下載浮水印內容，詳見 SPEC.md 第 5 節「主文下載浮水印」小節。
/// </summary>
public sealed record PdfWatermarkContent(
    string CompanyCode,
    string DocumentId);

public interface IPdfWatermarkService
{
    /// <summary>
    /// 讀取來源 PDF 的每一頁，疊加浮水印文字後回傳新的串流（已 Seek 回開頭）。
    /// 不修改任何儲存體上的原始檔案，只處理當下要回應給呼叫端的內容。
    /// </summary>
    Task<Stream> ApplyWatermarkAsync(
        Stream source,
        PdfWatermarkContent content,
        CancellationToken cancellationToken);
}
