using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class DocumentAccessHandler(IDocumentAccessStore documentAccessStore)
    : AuthorizationHandler<DocumentAccessRequirement, DocumentAccessResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentAccessRequirement requirement,
        DocumentAccessResource resource)
    {
        var role = Enum.TryParse<UserRole>(
            context.User.FindFirstValue(ClaimTypes.Role),
            ignoreCase: false,
            out var parsedRole)
            ? parsedRole
            : (UserRole?)null;
        var companyId = Guid.TryParse(
            context.User.FindFirstValue(AuthClaimTypes.CompanyId),
            out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var documentCompanyId = await documentAccessStore.FindDocumentCompanyIdAsync(
            resource.DocumentId, CancellationToken.None);

        if (documentCompanyId.HasValue
            && CompanyAccessRules.CanAccess(role, companyId, documentCompanyId.Value))
        {
            context.Succeed(requirement);
            return;
        }

        if (role is not UserRole.USER
            || !Guid.TryParse(
                context.User.FindFirstValue(AuthClaimTypes.DeptId),
                out var deptId))
        {
            return;
        }

        if (await documentAccessStore.DeptHasAccessAsync(
            resource.DocumentId, deptId, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
