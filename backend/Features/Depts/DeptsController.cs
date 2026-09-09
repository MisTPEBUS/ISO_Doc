using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Depts.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Depts;

[ApiController]
[Route("api/depts")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DeptsController(
    IDeptService deptService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<DeptResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (companyId is { } targetCompanyId
            && !await CanAccessCompanyAsync(targetCompanyId))
        {
            return Forbid();
        }

        return (await deptService.ListAsync(companyId, page, pageSize, cancellationToken))
            .ToOkResult(this);
    }

    [HttpPost]
    [ProducesResponseType<DeptResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateDeptRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(request.CompanyId))
        {
            return Forbid();
        }

        var result = await deptService.CreateAsync(request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } dept
            ? Url.ActionLink(nameof(Get), values: new { id = dept.Id })
            : null;
        return result.ToCreatedResult(this, location);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DeptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await deptService.GetAsync(id, cancellationToken)).ToOkResult(this);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<DeptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateDeptRequest request,
        CancellationToken cancellationToken) =>
        (await deptService.UpdateAsync(id, request, cancellationToken)).ToOkResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await deptService.DeleteAsync(id, cancellationToken)).ToNoContentResult(this);

    private async Task<bool> CanAccessCompanyAsync(Guid companyId)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User,
            new CompanyScopeResource(companyId),
            Policies.CompanyAdminScope);
        return authorization.Succeeded;
    }
}
