namespace IsoDocument.Api.Features.AiImport.Llm;

public sealed class AiImportLlmOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
