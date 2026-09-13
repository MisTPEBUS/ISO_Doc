namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class BulkImportDocumentsRequest
{
    public Guid CompanyId { get; init; }
    public IReadOnlyList<BulkImportDocumentItem>? Items { get; init; }
}

public sealed class BulkImportDocumentItem
{
    public string? DocumentNo { get; init; }
    public string? Name { get; init; }
    public int? PageCount { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public string? Version { get; init; }
}
