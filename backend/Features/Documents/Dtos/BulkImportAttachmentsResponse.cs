namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record BulkImportAttachmentsResponse(
    int Total,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<BulkImportAttachmentSuccess> Succeeded,
    IReadOnlyList<BulkImportAttachmentFailure> Failed);

public sealed record BulkImportAttachmentSuccess(
    int Index,
    BulkImportedAttachment Attachment);

public sealed record BulkImportedAttachment(
    Guid AttachmentId,
    Guid DocumentId,
    string DocumentNo,
    string? AttachmentNo,
    string Name);

public sealed record BulkImportAttachmentFailure(
    int Index,
    BulkImportAttachmentItem OriginalData,
    Dictionary<string, string[]> Errors);
