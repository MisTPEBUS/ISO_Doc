using System.Text.Json;
using IsoDocument.Api.Features.AiImport.Dtos;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;

namespace IsoDocument.Api.Features.AiImport.Llm;

// OpenAI .NET SDK 目前把 Responses API 標為 evaluation-only（OPENAI001）。
// gpt-5.6-terra 走的正是 Responses API + Structured Outputs，這裡是刻意使用，故整支停用該診斷。
#pragma warning disable OPENAI001
public sealed class OpenAiImportAnalyzer(IOptions<AiImportLlmOptions> options) : ILlmImportAnalyzer
{
    private const string ResponseSchema = """
        {
          "type": "object",
          "properties": {
            "files": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "relativePath": { "type": "string" },
                  "role": { "type": "string", "enum": ["MAIN", "ATTACHMENT", "UNRESOLVED"] },
                  "documentNo": { "type": ["string", "null"] },
                  "attachmentNo": { "type": ["string", "null"] },
                  "name": { "type": ["string", "null"] },
                  "effectiveDate": { "type": ["string", "null"] },
                  "confidence": { "type": "string", "enum": ["HIGH", "MEDIUM", "LOW"] },
                  "reason": { "type": ["string", "null"] }
                },
                "required": [
                  "relativePath", "role", "documentNo", "attachmentNo",
                  "name", "effectiveDate", "confidence", "reason"
                ],
                "additionalProperties": false
              }
            }
          },
          "required": ["files"],
          "additionalProperties": false
        }
        """;

    public async Task<IReadOnlyList<LlmFileSuggestion>> RefineAsync(
        IReadOnlyList<ImportFileDescriptor> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var client = new OpenAIClient(options.Value.ApiKey);
        var responsesClient = client.GetResponsesClient();

        var payload = JsonSerializer.Serialize(files.Select(file => new
        {
            relativePath = file.RelativePath,
            fileName = file.OriginalFileName,
            role = file.Role,
            documentNo = file.DocumentNo,
            attachmentNo = file.AttachmentNo,
            displayName = file.DisplayName,
            parseStatus = file.ParseStatus
        }));

        var prompt = $"""
            你是 ISO 文件管理系統的匯入助理。以下是使用者拖曳進來、經過本地規則初步解析的檔案清單（JSON 陣列）。
            請針對每一個檔案修正或確認它的分類，尤其是 parseStatus 為 WARNING 或 role 為 UNRESOLVED / MAIN_CANDIDATE 的項目：
            - role 只能是 MAIN（主文）、ATTACHMENT（附件）或 UNRESOLVED（無法判斷所屬主文）。
            - documentNo／attachmentNo 請盡量從檔名或路徑推斷；無法判斷時回傳 null。
            - effectiveDate 若能從檔名/內容判斷生效日期就填 yyyy-MM-dd，否則回傳 null。
            - confidence 反映你對這筆判斷的信心程度。
            對每一個輸入的檔案都要回傳一筆對應的結果，relativePath 必須與輸入完全一致。

            檔案清單：
            {payload}
            """;

        var createOptions = new CreateResponseOptions(
            options.Value.Model,
            [ResponseItem.CreateUserMessageItem(prompt)])
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "import_file_analysis",
                    BinaryData.FromString(ResponseSchema),
                    jsonSchemaFormatDescription: null,
                    jsonSchemaIsStrict: true)
            }
        };

        var result = await responsesClient.CreateResponseAsync(createOptions, cancellationToken);
        var outputText = result.Value.GetOutputText();

        using var document = JsonDocument.Parse(outputText);
        var suggestions = new List<LlmFileSuggestion>();
        foreach (var element in document.RootElement.GetProperty("files").EnumerateArray())
        {
            suggestions.Add(new LlmFileSuggestion(
                element.GetProperty("relativePath").GetString() ?? string.Empty,
                element.GetProperty("role").GetString() ?? ImportFileRoles.Unresolved,
                element.GetProperty("documentNo").GetString(),
                element.GetProperty("attachmentNo").GetString(),
                element.GetProperty("name").GetString(),
                DateOnly.TryParse(element.GetProperty("effectiveDate").GetString(), out var effectiveDate)
                    ? effectiveDate
                    : null,
                element.GetProperty("confidence").GetString() ?? "LOW",
                element.GetProperty("reason").GetString()));
        }

        return suggestions;
    }
}
#pragma warning restore OPENAI001
