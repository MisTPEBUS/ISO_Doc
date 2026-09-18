using IsoDocument.Api.Features.AiImport.Dtos;

namespace IsoDocument.Api.Features.AiImport.Llm;

public sealed record LlmFileSuggestion(
    string RelativePath,
    string Role,
    string? DocumentNo,
    string? AttachmentNo,
    string? Name,
    DateOnly? EffectiveDate,
    string Confidence,
    string? Reason);

public interface ILlmImportAnalyzer
{
    Task<IReadOnlyList<LlmFileSuggestion>> RefineAsync(
        IReadOnlyList<ImportFileDescriptor> files, CancellationToken cancellationToken);
}
