using IsoDocument.Api.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace IsoDocs.Tests.Storage;

public sealed class StorageHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRootExistsAndIsWritable_ReturnsHealthy()
    {
        using var temp = new TempDirectory();
        var check = new StorageHealthCheck(new StoragePathGuard(temp.Path));

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRootDoesNotExist_ReturnsUnhealthyWithoutCreatingIt()
    {
        using var temp = new TempDirectory();
        var missingRoot = System.IO.Path.Combine(temp.Path, "missing-mount");
        var check = new StorageHealthCheck(new StoragePathGuard(missingRoot));

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False(Directory.Exists(missingRoot));
    }

    private static HealthCheckContext CreateContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "storage",
            _ => throw new NotSupportedException(),
            failureStatus: null,
            tags: null)
    };
}
