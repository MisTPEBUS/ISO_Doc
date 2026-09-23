using IsoDocument.Api.Common;
using IsoDocument.Api.Features.IsoCategories.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.IsoCategories;

[ApiController]
[Route("api/iso-categories")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class IsoCategoriesController(
    IIsoCategoryService isoCategoryService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<IsoCategoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (companyId is { } targetCompanyId
            && !await CanAccessCompanyAsync(targetCompanyId))
        {
            return Forbid();
        }

        return (await isoCategoryService.ListAsync(
                companyId, includeInactive, page, pageSize, cancellationToken))
            .ToOkResult(this);
    }

    [HttpPost]
    [ProducesResponseType<IsoCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateIsoCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(request.CompanyId))
        {
            return Forbid();
        }

        var result = await isoCategoryService.CreateAsync(request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } category
            ? Url.ActionLink(nameof(Get), values: new { id = category.Id })
            : null;
        return result.ToCreatedResult(this, location);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<IsoCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await isoCategoryService.GetAsync(id, cancellationToken)).ToOkResult(this);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<IsoCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateIsoCategoryRequest request,
        CancellationToken cancellationToken) =>
        (await isoCategoryService.UpdateAsync(id, request, cancellationToken)).ToOkResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await isoCategoryService.DeleteAsync(id, cancellationToken)).ToNoContentResult(this);

    private async Task<bool> CanAccessCompanyAsync(Guid companyId)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User,
            new CompanyScopeResource(companyId),
            Policies.CompanyAdminScope);
        return authorization.Succeeded;
    }
}
