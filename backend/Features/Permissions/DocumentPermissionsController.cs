using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Permissions.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Permissions;

[ApiController]
[Route("api/documents/{documentId:guid}/dept-permissions")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DocumentPermissionsController(
    IDocumentPermissionService permissionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid documentId,
        CancellationToken cancellationToken) =>
        (await permissionService.GetAsync(documentId, cancellationToken)).ToOkResult(this);

    [HttpPut]
    public async Task<IActionResult> Update(
        Guid documentId,
        UpdateDocumentDeptPermissionsRequest request,
        CancellationToken cancellationToken) =>
        (await permissionService.UpdateAsync(documentId, request, cancellationToken))
        .ToOkResult(this);
}
