namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record CreateDocumentRequest(
    Guid CompanyId, string? DocumentNo, string? Name, Guid? IsoCategoryId = null, Guid? DeptId = null);
