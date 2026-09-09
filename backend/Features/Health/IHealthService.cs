namespace IsoDocument.Api.Features.Health;

public interface IHealthService
{
    Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken);
}
