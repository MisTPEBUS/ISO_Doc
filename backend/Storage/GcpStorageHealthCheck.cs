using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IsoDocument.Api.Storage;

public sealed class GcpStorageHealthCheck(
    StorageClient client,
    IOptions<StorageOptions> storageOptions,
    IOptions<GcpStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var enumerator = client.ListObjectsAsync(
                storageOptions.Value.BucketName,
                options.Value.ObjectPrefix.Trim('/') + "/",
                new ListObjectsOptions { PageSize = 1 })
                .GetAsyncEnumerator(cancellationToken);
            await enumerator.MoveNextAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "GCP storage bucket is not accessible.", exception);
        }
    }
}
