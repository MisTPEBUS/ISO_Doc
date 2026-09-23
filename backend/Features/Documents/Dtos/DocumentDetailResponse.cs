namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentDetailResponse(
    Guid Id,
    Guid CompanyId,
    string DocumentNo,
    string Name,
    bool IsActive,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? IsoCategoryId,
    Guid? DeptId,
    string? DeptName,
    DocumentVersionSummary? CurrentVersion,
    IReadOnlyList<DocumentVersionSummary> Versions,
    IReadOnlyList<DocumentAttachmentSummary> Attachments);
