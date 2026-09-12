using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/attachments")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class AttachmentsController(IAttachmentService attachmentService) : ControllerBase
{
    [HttpPut("{id:guid}/file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile(
        Guid id,
        [FromForm] UploadAttachmentFileRequest request,
        CancellationToken cancellationToken) =>
        (await attachmentService.UploadFileAsync(id, request, cancellationToken)).ToOkResult(this);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await attachmentService.DeleteAsync(id, cancellationToken)).ToNoContentResult(this);
}
