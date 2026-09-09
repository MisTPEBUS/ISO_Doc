using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Permissions.Dtos;

namespace IsoDocument.Api.Features.Permissions;

public interface IDocumentPermissionService
{
    Task<Result<DocumentDeptPermissionsResponse>> GetAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<Result<DocumentDeptPermissionsResponse>> UpdateAsync(
        Guid documentId,
        UpdateDocumentDeptPermissionsRequest request,
        CancellationToken cancellationToken);
}
