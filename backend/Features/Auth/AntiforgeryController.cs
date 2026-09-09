using Microsoft.AspNetCore.Authorization;
using IsoDocument.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Auth;

[ApiController]
[Route("api/antiforgery")]
public sealed class AntiforgeryController(IAuthSession authSession) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetToken()
    {
        authSession.IssueAntiforgeryToken();
        return NoContent();
    }
}
