using IsoDocument.Api.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace IsoDocument.Api.Security;

public sealed class ApiCookieAuthenticationEvents : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Authentication is required.");

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "You do not have permission to access this resource.");

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail)
    {
        var factory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = ApiProblem.Create(factory, context, status, title, detail);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
