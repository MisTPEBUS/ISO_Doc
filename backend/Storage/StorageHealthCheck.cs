using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IsoDocument.Api.Storage;

public sealed class StorageHealthCheck(StoragePathGuard pathGuard) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(pathGuard.RootPath))
        {
            return HealthCheckResult.Unhealthy("The configured storage root does not exist.");
        }

        var probePath = Path.Combine(pathGuard.RootPath, $".storage-health-{Guid.NewGuid():N}");
        try
        {
            await using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await probe.WriteAsync(new byte[] { 1 }, cancellationToken);
            await probe.FlushAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "The configured storage root is not writable.",
                exception);
        }
    }
}
