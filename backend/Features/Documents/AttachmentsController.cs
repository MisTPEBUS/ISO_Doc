using IsoDocument.Api.Common;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/attachments")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class AttachmentsController(IAttachmentService attachmentService) : ControllerBase
{
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await attachmentService.DeleteAsync(id, cancellationToken)).ToNoContentResult(this);
}
