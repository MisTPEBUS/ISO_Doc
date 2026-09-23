namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record UpdateDocumentRequest(string? Name, Guid? IsoCategoryId = null, Guid? DeptId = null);
