using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/documents/{documentId:guid}/attachments")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DocumentAttachmentsController(
    IAttachmentService attachmentService,
    IAuthorizationService authorizationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid documentId,
        CancellationToken cancellationToken) =>
        (await attachmentService.ListAsync(documentId, cancellationToken)).ToOkResult(this);

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid documentId,
        CreateAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attachmentService.CreateAsync(documentId, request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } attachment
            ? Url.ActionLink(nameof(Get), values: new { documentId, attachmentId = attachment.AttachmentId })
            : null;
        return result.ToCreatedResult(this, location);
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> Get(
        Guid documentId,
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        (await attachmentService.GetAsync(documentId, attachmentId, cancellationToken))
        .ToOkResult(this);

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid documentId,
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        (await attachmentService.DeleteAsync(documentId, attachmentId, cancellationToken))
        .ToNoContentResult(this);

    [HttpPost("/api/attachments/bulk-import")]
    public async Task<IActionResult> BulkImport(
        BulkImportAttachmentsRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User,
            new CompanyScopeResource(request.CompanyId),
            Policies.CompanyAdminScope);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        return (await attachmentService.BulkImportAsync(request, cancellationToken))
            .ToOkResult(this);
    }
}
