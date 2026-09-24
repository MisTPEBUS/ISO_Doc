using Microsoft.AspNetCore.Http;

namespace IsoDocument.Api.Features.AiImport.Dtos;

public sealed class CommitImportRequest
{
    public Guid CompanyId { get; init; }
    public Guid? AnalysisId { get; init; }
    public List<CommitImportDocumentItem>? Documents { get; init; }
}

public sealed class CommitImportDocumentItem
{
    public string? DocumentNo { get; init; }
    public string? Name { get; init; }
    public Guid? IsoCategoryId { get; init; }
    public Guid? DeptId { get; init; }
    public string? Version { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public int? PageCount { get; init; }
    public IFormFile? MainFile { get; init; }
    public List<CommitImportAttachmentItem>? Attachments { get; init; }
}

public sealed class CommitImportAttachmentItem
{
    public string? AttachmentNo { get; init; }
    public string? Name { get; init; }
    public string? Version { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public IFormFile? File { get; init; }
}
