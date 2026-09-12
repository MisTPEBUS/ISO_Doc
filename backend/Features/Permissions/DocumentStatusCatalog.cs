using IsoDocument.Api.Features.Permissions.Dtos;

namespace IsoDocument.Api.Features.Permissions;

/// <summary>
/// 文件狀態的 code 列舉與對應中文 label（後端固定吐出，前端不自行推算）。
/// label 文字為本專案定義，SPEC 未逐一列出時以此為準。
/// </summary>
public static class DocumentStatusCatalog
{
    public static class MainDocumentCode
    {
        public const string Normal = "NORMAL";
        public const string Error = "ERROR";
        public const string Missing = "MISSING";
    }

    public static class AttachmentCode
    {
        public const string Normal = "NORMAL";
        public const string Error = "ERROR";
        public const string Missing = "MISSING";
        public const string None = "NONE";
    }

    public static class EffectiveCode
    {
        public const string Draft = "DRAFT";
        public const string Scheduled = "SCHEDULED";
        public const string Effective = "EFFECTIVE";

        // EXPIRED 目前邏輯上不會產生（保留列舉值，見 SPEC「Document 狀態計算」）。
        public const string Expired = "EXPIRED";
        public const string Cancelled = "CANCELLED";
    }

    private static readonly IReadOnlyDictionary<string, string> MainDocumentLabels =
        new Dictionary<string, string>
        {
            [MainDocumentCode.Normal] = "主文正常",
            [MainDocumentCode.Error] = "主文檔有誤",
            [MainDocumentCode.Missing] = "主文未上傳",
        };

    private static readonly IReadOnlyDictionary<string, string> AttachmentLabels =
        new Dictionary<string, string>
        {
            [AttachmentCode.Normal] = "附件正常",
            [AttachmentCode.Error] = "附件檔有誤",
            [AttachmentCode.Missing] = "附件未補齊",
            [AttachmentCode.None] = "無附件",
        };

    private static readonly IReadOnlyDictionary<string, string> EffectiveLabels =
        new Dictionary<string, string>
        {
            [EffectiveCode.Draft] = "草稿",
            [EffectiveCode.Scheduled] = "尚未生效",
            [EffectiveCode.Effective] = "已生效",
            [EffectiveCode.Expired] = "已失效",
            [EffectiveCode.Cancelled] = "已作廢",
        };

    public static DocumentFileStatusResponse MainDocument(string code) =>
        new(code, MainDocumentLabels[code], HasError: code == MainDocumentCode.Error);

    public static DocumentFileStatusResponse Attachment(string code) =>
        new(code, AttachmentLabels[code], HasError: code == AttachmentCode.Error);

    public static DocumentEffectiveStatusResponse Effective(string code) =>
        new(code, EffectiveLabels[code]);
}
