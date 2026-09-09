using IsoDocument.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Health;

public sealed class HealthService(
    IsoDbContext dbContext,
    ILogger<HealthService> logger) : IHealthService
{
    public async Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PostgreSQL health check failed.");
            return false;
        }
    }
}
