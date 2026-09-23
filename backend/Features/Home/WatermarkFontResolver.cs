using System.Reflection;
using PdfSharp.Fonts;

namespace IsoDocument.Api.Features.Home;

/// <summary>
/// 主文下載浮水印用的字型解析器。PDFsharp 6.x 不再依賴作業系統字型（GDI-free），
/// 不論在 Windows 開發機或 Linux NAS Docker 容器上都必須自行提供字型位元組。
/// 依請求的字型家族名稱分派到兩個內嵌字型（皆為 TrueType variable font，
/// SIL OFL 1.1，授權原文見同目錄各自的 LICENSE 檔）：
/// - <see cref="ChineseFamilyName"/>：Noto Sans TC，用於可能含中文的文字（文件名稱／編號）。
/// - <see cref="LatinFamilyName"/>：Lora，用於正中央公司代碼（固定為 ASCII，見 companies.code
///   的 `^[A-Z0-9]{2,30}$` 驗證規則）。長度上限拉到 30 後，正中央 64pt 的長代碼有可能超出
///   頁寬，這是已知的版面風險，目前沒有自動縮字或換行處理。
/// 兩個字型檔都只內嵌了「預設字重」這一個實例，沒有另外包裝真正的 Bold 字重檔，
/// 所以 Bold 一律靠 PDFsharp 的 <c>MustSimulateBold</c>（合成粗體／描邊加粗）達成，
/// 不是字型本身的 Bold 字重。
/// </summary>
public sealed class WatermarkFontResolver : IFontResolver
{
    public const string ChineseFamilyName = "Noto Sans TC";
    public const string LatinFamilyName = "Lora";

    private const string ChineseFaceName = "NotoSansTC#Regular";
    private const string LatinFaceName = "Lora#Regular";

    private static readonly Lazy<byte[]> ChineseFontBytes = new(
        () => LoadFontBytes("NotoSansTC-Variable.ttf"));
    private static readonly Lazy<byte[]> LatinFontBytes = new(
        () => LoadFontBytes("Lora-Variable.ttf"));

    public byte[] GetFont(string faceName) => faceName switch
    {
        LatinFaceName => LatinFontBytes.Value,
        _ => ChineseFontBytes.Value
    };

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        familyName == LatinFamilyName
            ? new FontResolverInfo(LatinFaceName, isBold, isItalic)
            : new FontResolverInfo(ChineseFaceName, isBold, isItalic);

    private static byte[] LoadFontBytes(string embeddedFileName)
    {
        var assembly = typeof(WatermarkFontResolver).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(embeddedFileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"找不到內嵌字型資源 {embeddedFileName}，請確認 csproj 的 EmbeddedResource 設定。");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"無法開啟內嵌字型資源 {resourceName}。");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
