using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/attachments/{attachmentId:guid}/versions")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class AttachmentVersionsController(
    IAttachmentVersionService attachmentVersionService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        Guid attachmentId,
        [FromForm] CreateAttachmentVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attachmentVersionService.CreateAsync(
            attachmentId, request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } version
            ? Url.ActionLink(
                action: nameof(Get),
                values: new { attachmentId, versionId = version.VersionId })
            : null;
        return result.ToCreatedResult(this, location);
    }

    [HttpGet("{versionId:guid}")]
    public async Task<IActionResult> Get(
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (await attachmentVersionService.GetAsync(attachmentId, versionId, cancellationToken))
        .ToOkResult(this);
}
