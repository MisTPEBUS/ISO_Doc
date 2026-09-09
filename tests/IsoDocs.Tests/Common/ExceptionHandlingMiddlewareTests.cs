using System.Text.Json;
using IsoDocument.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Common;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenDomainExceptionIsThrown_ReturnsMatchingProblemDetails()
    {
        using var services = CreateServices();
        var context = CreateContext(services);
        var middleware = CreateMiddleware(
            services,
            _ => throw new DomainException(Result.Conflict(
                "The department still has active users.",
                "Department cannot be deleted")));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        using var body = await ReadBodyAsync(context);
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "Department cannot be deleted",
            body.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "The department still has active users.",
            body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedExceptionIsThrown_DoesNotExposeInternalDetails()
    {
        const string sensitiveDetail = "connection password=top-secret at Internal.Service:123";
        using var services = CreateServices();
        var context = CreateContext(services);
        var middleware = CreateMiddleware(
            services,
            _ => throw new InvalidOperationException(sensitiveDetail));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        var responseJson = body.RootElement.GetRawText();
        Assert.DoesNotContain(sensitiveDetail, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", responseJson, StringComparison.Ordinal);
        Assert.Equal(
            "An unexpected error occurred while processing the request.",
            body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationDomainExceptionIsThrown_ReturnsFieldErrors()
    {
        using var services = CreateServices();
        var context = CreateContext(services);
        var middleware = CreateMiddleware(
            services,
            _ => throw new DomainException(Result.ValidationFailed(
                new Dictionary<string, string[]>
                {
                    ["name"] = ["Name is required."]
                })));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal("Name is required.", errors.GetProperty("name")[0].GetString());
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(
        IServiceProvider services,
        RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(
            next,
            services.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>(),
            services.GetRequiredService<ProblemDetailsFactory>());
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddControllers();
        return services.BuildServiceProvider();
    }
}
