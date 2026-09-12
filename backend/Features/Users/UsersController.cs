using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Users.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Users;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class UsersController(
    IUserService userService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? deptId,
        [FromQuery] string? keyword,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (companyId is { } targetCompanyId && !await CanAccessCompanyAsync(targetCompanyId))
        {
            return Forbid();
        }

        return (await userService.ListAsync(
            companyId, deptId, keyword, includeInactive, page, pageSize, cancellationToken))
            .ToOkResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(request.CompanyId))
        {
            return Forbid();
        }

        var result = await userService.CreateAsync(request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } user
            ? Url.ActionLink(nameof(Get), values: new { id = user.Id })
            : null;
        return result.ToCreatedResult(this, location);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> BatchCreate(
        BatchCreateUsersRequest request,
        CancellationToken cancellationToken) =>
        (await userService.BatchCreateAsync(request, cancellationToken)).ToOkResult(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await userService.GetAsync(id, cancellationToken)).ToOkResult(this);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        (await userService.UpdateAsync(id, request, cancellationToken)).ToOkResult(this);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await userService.DeleteAsync(id, cancellationToken)).ToNoContentResult(this);

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken) =>
        (await userService.ResetPasswordAsync(id, cancellationToken)).ToOkResult(this);

    private async Task<bool> CanAccessCompanyAsync(Guid companyId)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User, new CompanyScopeResource(companyId), Policies.CompanyAdminScope);
        return authorization.Succeeded;
    }
}
