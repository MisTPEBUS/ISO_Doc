namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record BulkImportDocumentsResponse(
    int Total,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<BulkImportDocumentSuccess> Succeeded,
    IReadOnlyList<BulkImportDocumentFailure> Failed);

public sealed record BulkImportDocumentSuccess(
    int Index,
    BulkImportedDocument Document);

public sealed record BulkImportedDocument(
    Guid DocumentId,
    Guid DocumentVersionId,
    string DocumentNo,
    string Name,
    int? PageCount,
    DateOnly? EffectiveDate,
    string Version,
    string Status);

public sealed record BulkImportDocumentFailure(
    int Index,
    BulkImportDocumentItem OriginalData,
    Dictionary<string, string[]> Errors);
