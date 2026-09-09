using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents;

public interface IDocumentVersionService
{
    Task<Result<DocumentVersionResponse>> CreateAsync(
        Guid documentId,
        CreateDocumentVersionRequest request,
        CancellationToken cancellationToken);
}
