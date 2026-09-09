using IsoDocument.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IsoDocs.Tests.Common;

public sealed class ResultActionExtensionsTests
{
    [Fact]
    public void ToOkResult_WhenResultIsSuccessful_ReturnsOk()
    {
        using var services = CreateServices();
        var controller = CreateController(services);

        var action = Result.Success().ToOkResult(controller);

        Assert.IsType<OkResult>(action);
    }

    [Fact]
    public void ToOkResult_WhenGenericResultIsSuccessful_ReturnsPayload()
    {
        using var services = CreateServices();
        var controller = CreateController(services);
        var payload = new TestPayload(42);

        var action = Result<TestPayload>.Success(payload).ToOkResult(controller);

        var ok = Assert.IsType<OkObjectResult>(action);
        Assert.Same(payload, ok.Value);
    }

    [Fact]
    public void ToCreatedResult_WhenResultIsSuccessful_ReturnsCreatedWithPayload()
    {
        using var services = CreateServices();
        var controller = CreateController(services);
        var payload = new TestPayload(42);

        var action = Result<TestPayload>.Success(payload)
            .ToCreatedResult(controller, "/api/resources/42");

        var created = Assert.IsType<CreatedResult>(action);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal("/api/resources/42", created.Location);
        Assert.Same(payload, created.Value);
    }

    [Fact]
    public void ToNoContentResult_WhenResultIsSuccessful_ReturnsNoContent()
    {
        using var services = CreateServices();
        var controller = CreateController(services);

        var action = Result.Success().ToNoContentResult(controller);

        Assert.IsType<NoContentResult>(action);
    }

    [Theory]
    [MemberData(nameof(FailedResults))]
    public void ToOkResult_WhenResultFailed_ReturnsExpectedProblem(
        Result result,
        int expectedStatus)
    {
        using var services = CreateServices();
        var controller = CreateController(services);

        var action = result.ToOkResult(controller);

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problem.Status);
    }

    [Fact]
    public void ToOkResult_WhenValidationFailed_ReturnsFieldErrors()
    {
        using var services = CreateServices();
        var controller = CreateController(services);
        var errors = new Dictionary<string, string[]>
        {
            ["name"] = ["Name is required."]
        };

        var action = Result.ValidationFailed(errors).ToOkResult(controller);

        var objectResult = Assert.IsType<ObjectResult>(action);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(errors["name"], problem.Errors["name"]);
    }

    public static TheoryData<Result, int> FailedResults => new()
    {
        { Result.NotFound(), StatusCodes.Status404NotFound },
        { Result.Conflict(), StatusCodes.Status409Conflict },
        { Result.Forbidden(), StatusCodes.Status403Forbidden },
        {
            Result.ValidationFailed(new Dictionary<string, string[]>
            {
                ["field"] = ["Invalid value."]
            }),
            StatusCodes.Status400BadRequest
        },
        { Result.Unauthorized(), StatusCodes.Status401Unauthorized }
    };

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddControllers();
        return services.BuildServiceProvider();
    }

    private static TestController CreateController(IServiceProvider services)
    {
        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services
                }
            }
        };
    }

    private sealed class TestController : ControllerBase;

    private sealed record TestPayload(int Id);
}
