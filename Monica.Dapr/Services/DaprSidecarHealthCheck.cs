using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.Dapr.Abstractions;
using Monica.Dapr.Models;

namespace Monica.Dapr.Services;

/// <summary>
/// Projects the coordinator's already-observed sidecar state into ASP.NET Core readiness.
/// </summary>
/// <remarks>
/// This adapter deliberately performs no network I/O. The coordinator remains the single owner of Dapr probing,
/// retry, and fail-fast behavior.
/// </remarks>
internal sealed class DaprSidecarHealthCheck(IDaprSidecarHealthCoordinator coordinator) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["status"] = coordinator.Status.ToString(),
            ["consecutiveFailures"] = coordinator.ConsecutiveFailures
        };

        if (coordinator.LastHealthyAt is { } lastHealthyAt)
        {
            data["lastHealthyAt"] = lastHealthyAt;
        }

        var result = coordinator.Status switch
        {
            DaprHealthStatus.Healthy => HealthCheckResult.Healthy(
                "The Dapr sidecar is ready.",
                data),
            DaprHealthStatus.Degraded => HealthCheckResult.Degraded(
                "The Dapr sidecar is responding with degraded reliability.",
                data: data),
            _ => HealthCheckResult.Unhealthy(
                $"The Dapr sidecar is not ready ({coordinator.Status}).",
                data: data)
        };

        return Task.FromResult(result);
    }
}
