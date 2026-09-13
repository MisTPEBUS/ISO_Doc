using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class AttachmentAccessHandler(IDocumentAccessStore documentAccessStore)
    : AuthorizationHandler<AttachmentAccessRequirement, AttachmentAccessResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AttachmentAccessRequirement requirement,
        AttachmentAccessResource resource)
    {
        var documentId = await documentAccessStore.FindAttachmentDocumentIdAsync(
            resource.AttachmentId, CancellationToken.None);
        if (documentId is null)
        {
            return;
        }

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
            documentId.Value, CancellationToken.None);

        if (documentCompanyId.HasValue
            && CompanyAccessRules.CanAccess(role, companyId, documentCompanyId.Value))
        {
            context.Succeed(requirement);
            return;
        }

        if (role is UserRole.USER
            && Guid.TryParse(context.User.FindFirstValue(AuthClaimTypes.DeptId), out var deptId)
            && await documentAccessStore.DeptHasAccessAsync(
                documentId.Value, deptId, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
