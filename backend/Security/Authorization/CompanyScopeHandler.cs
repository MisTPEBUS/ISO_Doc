using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class CompanyScopeHandler
    : AuthorizationHandler<CompanyScopeRequirement, CompanyScopeResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CompanyScopeRequirement requirement,
        CompanyScopeResource resource)
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

        if (CompanyAccessRules.CanAccess(role, companyId, resource.TargetCompanyId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
