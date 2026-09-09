using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/documents/{documentId:guid}/versions")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DocumentVersionsController(
    IDocumentVersionService documentVersionService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        Guid documentId,
        [FromForm] CreateDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await documentVersionService.CreateAsync(
            documentId, request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } version
            ? Url.ActionLink(
                action: "Get",
                controller: "DocumentVersions",
                values: new { documentId, versionId = version.VersionId })
            : null;
        return result.ToCreatedResult(this, location);
    }
}
