using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class SystemAdminHandler : AuthorizationHandler<SystemAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SystemAdminRequirement requirement)
    {
        if (context.User.IsInRole(nameof(UserRole.SYSTEM_ADMIN)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
