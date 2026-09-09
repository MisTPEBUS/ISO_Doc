using System.Security.Claims;
using IsoDocument.Api.Security;
using IsoDocument.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace IsoDocs.Tests.Security;

public sealed class CompanyScopeHandlerTests
{
    private static readonly Guid OwnCompanyId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid OtherCompanyId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(UserRole.COMPANY_ADMIN, true, true)]
    [InlineData(UserRole.COMPANY_ADMIN, false, false)]
    [InlineData(UserRole.SYSTEM_ADMIN, false, true)]
    [InlineData(UserRole.USER, true, false)]
    public async Task HandleAsync_AppliesCompanyScopeRules(
        UserRole role,
        bool useOwnCompany,
        bool expectedSuccess)
    {
        var requirement = new CompanyScopeRequirement();
        var claims = new Claim[]
        {
            new(ClaimTypes.Role, role.ToString()),
            new(AuthClaimTypes.CompanyId, OwnCompanyId.ToString())
        };
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")),
            new CompanyScopeResource(useOwnCompany ? OwnCompanyId : OtherCompanyId));
        var handler = new CompanyScopeHandler();

        await handler.HandleAsync(context);

        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }
}
