using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace IsoDocument.Api.Common;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    ProblemDetailsFactory problemDetailsFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteDomainProblemAsync(context, exception.Result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing the request.");
        }
    }

    private Task WriteDomainProblemAsync(HttpContext context, Result result)
    {
        var status = result.Status.ToHttpStatusCode();

        return WriteProblemAsync(
            context,
            status,
            result.Title ?? "Domain Rule Violation",
            result.Detail,
            result.Errors);
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string? detail,
        Dictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("The response has already started.");
        }

        var problem = ApiProblem.Create(
            problemDetailsFactory,
            context,
            status,
            title,
            detail,
            errors);

        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
