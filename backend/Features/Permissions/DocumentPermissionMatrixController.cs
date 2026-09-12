using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Permissions.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Permissions;

[ApiController]
[Route("api/documents/permission-matrix")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DocumentPermissionMatrixController(
    IDocumentPermissionMatrixService matrixService,
    IDocumentPermissionService permissionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DocumentPermissionMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? companyId,
        [FromQuery] string? companyCode,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new DocumentPermissionMatrixQuery
        {
            CompanyId = companyId,
            CompanyCode = companyCode,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };

        return (await matrixService.GetMatrixAsync(query, cancellationToken)).ToOkResult(this);
    }

    [HttpPut]
    [ProducesResponseType<UpdateDocumentPermissionMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBatch(
        [FromBody] UpdateDocumentPermissionMatrixRequest request,
        CancellationToken cancellationToken) =>
        (await permissionService.UpdateMatrixAsync(request, cancellationToken)).ToOkResult(this);
}
