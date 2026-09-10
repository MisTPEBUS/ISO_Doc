using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Companies.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Companies;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Policies.SystemAdmin)]
public sealed class CompaniesController(ICompanyService companyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<CompanyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        (await companyService.ListAsync(keyword, page, pageSize, cancellationToken))
            .ToOkResult(this);
}
