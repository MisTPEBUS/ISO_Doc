namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class BulkImportAttachmentsRequest
{
    public Guid CompanyId { get; init; }
    public IReadOnlyList<BulkImportAttachmentItem>? Items { get; init; }
}

public sealed class BulkImportAttachmentItem
{
    public string? DocumentNo { get; init; }
    public string? AttachmentNo { get; init; }
    public string? Name { get; init; }
}
