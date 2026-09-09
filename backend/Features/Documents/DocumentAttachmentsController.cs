using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/documents/{documentId:guid}/versions/{versionId:guid}/attachments")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DocumentAttachmentsController(IAttachmentService attachmentService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (await attachmentService.ListAsync(documentId, versionId, cancellationToken))
        .ToOkResult(this);

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        Guid documentId,
        Guid versionId,
        [FromForm] CreateAttachmentsRequest request,
        CancellationToken cancellationToken) =>
        (await attachmentService.CreateAsync(
            documentId, versionId, request, cancellationToken))
        .ToCreatedResult(this);
}
