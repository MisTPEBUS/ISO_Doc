using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using IsoDocument.Api.Storage;

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
        catch (GcpStorageUnavailableException exception)
        {
            logger.LogWarning(exception, "GCP storage upload is temporarily unavailable.");
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "檔案儲存暫時無法使用",
                "上傳未完成，請稍後重新整理並確認版本狀態後再試一次。",
                retryAfterSeconds: 5);
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
                "伺服器發生錯誤",
                "處理要求時發生未預期的錯誤。");
        }
    }

    private Task WriteDomainProblemAsync(HttpContext context, Result result)
    {
        var status = result.Status.ToHttpStatusCode();

        return WriteProblemAsync(
            context,
            status,
            result.Title ?? "違反業務規則",
            result.Detail,
            result.Errors);
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string? detail,
        Dictionary<string, string[]>? errors = null,
        int? retryAfterSeconds = null)
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
        if (retryAfterSeconds is not null)
        {
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }
        await context.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
