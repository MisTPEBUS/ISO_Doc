using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Permissions.Dtos;

namespace IsoDocument.Api.Features.Permissions;

public interface IDocumentPermissionMatrixService
{
    Task<Result<DocumentPermissionMatrixResponse>> GetMatrixAsync(
        DocumentPermissionMatrixQuery query,
        CancellationToken cancellationToken);
}
