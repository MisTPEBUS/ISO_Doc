using IsoDocument.Api.Common;
using IsoDocument.Api.Features.AiImport.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.AiImport;

[ApiController]
[Route("api/documents/ai-import")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class AiImportController(
    IAiImportService aiImportService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(
        AnalyzeImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(request.CompanyId))
        {
            return Forbid();
        }

        return (await aiImportService.AnalyzeAsync(request, cancellationToken)).ToOkResult(this);
    }

    [HttpPost("commit")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Commit(
        [FromForm] CommitImportRequest request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("/1232131231232133333333333333333333333");
        if (!await CanAccessCompanyAsync(request.CompanyId))
        {
            return Forbid();
        }

        return (await aiImportService.CommitAsync(request, cancellationToken)).ToOkResult(this);
    }

    private async Task<bool> CanAccessCompanyAsync(Guid companyId)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User, new CompanyScopeResource(companyId), Policies.CompanyAdminScope);
        return authorization.Succeeded;
    }
}
