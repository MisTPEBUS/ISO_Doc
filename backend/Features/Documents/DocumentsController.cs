using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDocument.Api.Features.Documents;

[ApiController]
[Route("api/documents")]
[Authorize(Policy = Policies.CompanyAdminScope)]
public sealed class DocumentsController(
    IDocumentService documentService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (companyId is { } targetCompanyId && !await CanAccessCompanyAsync(targetCompanyId))
        {
            return Forbid();
        }

        return (await documentService.ListAsync(
            companyId, keyword, page, pageSize, cancellationToken)).ToOkResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(request.CompanyId))
        {
            return Forbid();
        }

        var result = await documentService.CreateAsync(request, cancellationToken);
        var location = result.IsSuccess && result.Value is { } document
            ? Url.ActionLink(nameof(Get), values: new { id = document.Id })
            : null;
        return result.ToCreatedResult(this, location);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await documentService.GetAsync(id, cancellationToken)).ToOkResult(this);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateDocumentRequest request,
        CancellationToken cancellationToken) =>
        (await documentService.UpdateAsync(id, request, cancellationToken)).ToOkResult(this);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await documentService.DeleteAsync(id, cancellationToken)).ToNoContentResult(this);

    private async Task<bool> CanAccessCompanyAsync(Guid companyId)
    {
        var authorization = await authorizationService.AuthorizeAsync(
            User, new CompanyScopeResource(companyId), Policies.CompanyAdminScope);
        return authorization.Succeeded;
    }
}
