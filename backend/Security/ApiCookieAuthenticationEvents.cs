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
            "尚未登入",
            "請先登入後再操作。");

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "沒有權限",
            "您沒有存取此資源的權限。");

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
