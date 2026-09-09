using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Features.Health;
using IsoDocument.Api.Features.Health.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Health;

public sealed class HealthApiTests
{
    [Fact]
    public async Task Get_WhenDatabaseIsHealthy_ReturnsOk()
    {
        await using var factory = CreateFactory(isHealthy: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new HealthResponse("Healthy", "Healthy"), body);
    }

    [Fact]
    public async Task Get_WhenDatabaseIsUnhealthy_ReturnsServiceUnavailable()
    {
        await using var factory = CreateFactory(isHealthy: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(new HealthResponse("Unhealthy", "Unhealthy"), body);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool isHealthy)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHealthService>();
                services.AddSingleton<IHealthService>(new StubHealthService(isHealthy));
            });
        });
    }

    private sealed class StubHealthService(bool isHealthy) : IHealthService
    {
        public Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(isHealthy);
        }
    }
}
