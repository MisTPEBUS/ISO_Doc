namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record CreateDocumentWithVersionResponse(
    DocumentResponse Document,
    DocumentVersionResponse Version);
