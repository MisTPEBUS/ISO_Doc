using System.Security.Claims;
using IsoDocument.Api.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;

namespace IsoDocument.Api.Security;

public sealed class CookieAuthSession(
    IHttpContextAccessor httpContextAccessor,
    IAntiforgery antiforgery) : IAuthSession
{
    public async Task SignInAsync(User user)
    {
        var httpContext = GetHttpContext();
        var claims = new Claim[]
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(AuthClaimTypes.CompanyId, user.CompanyId.ToString()),
            new(AuthClaimTypes.DeptId, user.DeptId.ToString()),
            new(AuthClaimTypes.Empno, user.Empno),
            new(AuthClaimTypes.MustChangePassword, user.MustChangePassword.ToString())
        };
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });
        httpContext.User = principal;
        IssueAntiforgeryToken();
    }

    public Task SignOutAsync() => GetHttpContext().SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    public void IssueAntiforgeryToken()
    {
        var httpContext = GetHttpContext();
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (tokens.RequestToken is null)
        {
            throw new InvalidOperationException("The antiforgery request token could not be generated.");
        }

        httpContext.Response.Cookies.Append(
            "isodocs.xsrf",
            tokens.RequestToken,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/"
            });
    }

    private HttpContext GetHttpContext() =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("An HTTP context is required for cookie authentication.");
}
