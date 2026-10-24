using FlashStream.Application.Contracts;

namespace FlashStream.Application.Services;

public sealed class HealthService(IApplicationHealth health) : IHealthService
{
    public async Task<HealthResult> CheckAsync(CancellationToken cancellationToken) =>
        new(await health.IsDatabaseAvailableAsync(cancellationToken));
}
