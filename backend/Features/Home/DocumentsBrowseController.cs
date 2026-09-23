using IsoDocument.Api.Common;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Home;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsBrowseController(
    IDocumentsBrowseService browseService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("available")]
    public async Task<IActionResult> Available(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? isoCategoryId = null,
        CancellationToken cancellationToken = default) =>
        (await browseService.ListAvailableAsync(
            page, pageSize, keyword, isoCategoryId, cancellationToken)).ToOkResult(this);

    [HttpGet("{documentId:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessDocumentAsync(documentId))
        {
            return Forbid();
        }

        var result = await browseService.DownloadDocumentAsync(
            documentId, versionId, cancellationToken);
        return ToFileResult(result);
    }

    [HttpGet("/api/attachments/{attachmentId:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User,
            new AttachmentAccessResource(attachmentId),
            Policies.AttachmentAccess);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        var result = await browseService.DownloadAttachmentAsync(
            attachmentId, versionId, cancellationToken);
        return ToFileResult(result);
    }

    private async Task<bool> CanAccessDocumentAsync(Guid documentId)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User,
            new DocumentAccessResource(documentId),
            Policies.DocumentAccess);
        return authorization.Succeeded;
    }

    private IActionResult ToFileResult(Result<Dtos.DownloadFileResponse> result)
    {
        if (!result.IsSuccess)
        {
            return result.ToOkResult(this);
        }

        var file = result.Value!;
        return File(file.Content, file.ContentType, file.FileName);
    }
}
