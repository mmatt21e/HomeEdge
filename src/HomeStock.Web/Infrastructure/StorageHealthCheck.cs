using HomeStock.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HomeStock.Web.Infrastructure;

/// <summary>Reports whether the attachment storage root exists and is writable.</summary>
public class StorageHealthCheck(IFileStorageService storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var healthy = storage.IsHealthy(out var detail);
        return Task.FromResult(healthy
            ? HealthCheckResult.Healthy($"Storage writable ({detail})")
            : HealthCheckResult.Unhealthy($"Storage not writable: {detail}"));
    }
}
