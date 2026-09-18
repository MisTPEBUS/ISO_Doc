namespace IsoDocument.Api.Features.AiImport.Dtos;

public sealed record CommitImportResponse(
    CommitImportDocumentsResult Documents,
    CommitImportAttachmentsResult Attachments);

public sealed record CommitImportDocumentsResult(
    int Total,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<CommitImportDocumentSuccess> Succeeded,
    IReadOnlyList<CommitImportDocumentFailure> Failed);

public sealed record CommitImportDocumentSuccess(
    int Index,
    string DocumentNo,
    Guid DocumentId,
    string Action,
    Guid? DocumentVersionId,
    string? Version,
    DateOnly? EffectiveDate,
    string? Status);

public sealed record CommitImportDocumentFailure(
    int Index,
    string? DocumentNo,
    Dictionary<string, string[]> Errors);

public sealed record CommitImportAttachmentsResult(
    int Total,
    int SuccessCount,
    int SkippedCount,
    int FailureCount,
    IReadOnlyList<CommitImportAttachmentSuccess> Succeeded,
    IReadOnlyList<CommitImportAttachmentSkipped> Skipped,
    IReadOnlyList<CommitImportAttachmentFailure> Failed);

public sealed record CommitImportAttachmentSuccess(
    int Index,
    int DocumentIndex,
    string? AttachmentNo,
    Guid AttachmentId,
    string Action,
    Guid? AttachmentVersionId,
    string? Version);

public sealed record CommitImportAttachmentSkipped(
    int Index,
    int DocumentIndex,
    string? AttachmentNo,
    string Reason);

public sealed record CommitImportAttachmentFailure(
    int Index,
    int DocumentIndex,
    string? AttachmentNo,
    Dictionary<string, string[]> Errors);
