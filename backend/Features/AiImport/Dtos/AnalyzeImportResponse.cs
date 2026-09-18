namespace IsoDocument.Api.Features.AiImport.Dtos;

public sealed record AnalyzeImportResponse(
    Guid AnalysisId,
    IReadOnlyList<AnalyzedDocument> Documents,
    IReadOnlyList<UnresolvedImportFile> Unresolved);

public sealed record ExistingDocumentState(
    Guid? DocumentId,
    string? LatestVersion,
    string? LatestVersionStatus,
    bool LatestVersionHasFile);

public sealed record ExistingAttachmentState(
    Guid? AttachmentId,
    string? LatestVersion);

public sealed record AnalyzedMainFile(string RelativePath);

public sealed record AnalyzedAttachment(
    string? AttachmentNo,
    string Name,
    DateOnly? EffectiveDate,
    string? SuggestedVersion,
    string PredictedAction,
    string RelativePath,
    ExistingAttachmentState Existing);

public sealed record AnalyzedDocument(
    string DocumentNo,
    string Name,
    DateOnly? EffectiveDate,
    string? SuggestedVersion,
    string PredictedAction,
    string Confidence,
    ExistingDocumentState Existing,
    AnalyzedMainFile? MainFile,
    IReadOnlyList<AnalyzedAttachment> Attachments);

public sealed record UnresolvedImportFile(string RelativePath, string Reason);
