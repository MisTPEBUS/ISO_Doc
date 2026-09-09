using IsoDocument.Api.Common;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace IsoDocument.Api.Features.Backup;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class BackupController(
    IBackupService backupService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("{companyId:guid}/backup")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User,
            new CompanyScopeResource(companyId),
            Policies.CompanyAdminScope);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        var result = await backupService.PrepareAsync(companyId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToOkResult(this);
        }

        var backup = result.Value!;
        Response.ContentType = "application/zip";
        Response.GetTypedHeaders().ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = backup.FileName
        };
        await backupService.WriteAsync(backup, Response.Body, cancellationToken);
        return new EmptyResult();
    }
}
