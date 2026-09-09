using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents;

public interface IDocumentService
{
    Task<Result<PagedResult<DocumentResponse>>> ListAsync(
        Guid? companyId, string? keyword, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<Result<DocumentResponse>> CreateAsync(
        CreateDocumentRequest request, CancellationToken cancellationToken);
    Task<Result<DocumentDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<DocumentResponse>> UpdateAsync(
        Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
