using Microsoft.Extensions.Diagnostics.HealthChecks;
using MoLibrary.Core.Features.HostedServices.Interfaces;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.WorkerPlane;

namespace MoLibrary.JobScheduler.HealthChecks;

/// <summary>
/// Health check for monitoring JobScheduler hosted services initialization status using the observable hosted service manager
/// </summary>
public class JobSchedulerHealthCheck(IMoHostedServiceManager serviceManager) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var serviceTypes = new[]
        {
            typeof(JobConcurrencyGuardHostedService),
            typeof(JobSchedulerHostedService),
            typeof(JobRegistrationHostedService),
            typeof(JobWorkerManager)
        };

        var services = serviceTypes
            .Select(type => serviceManager.GetService(type))
            .Where(info => info != null)
            .ToList();

        if (services.Count == 0)
        {
            // No services are registered, this is unusual but not necessarily unhealthy
            return Task.FromResult(HealthCheckResult.Healthy(
                "No JobScheduler services are registered"));
        }

        // Check if any service is faulted
        var faultedServices = services.Where(s => s!.IsFaulted).ToList();
        if (faultedServices.Any())
        {
            var errors = string.Join("; ", faultedServices.Select(s =>
            {
                var lastError = s!.StateHistory
                    .Where(h => h.Exception != null)
                    .OrderByDescending(h => h.Timestamp)
                    .FirstOrDefault();
                return $"{s.ServiceName}: {lastError?.Message ?? "Unknown error"}";
            }));

            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"JobScheduler services failed: {errors}"));
        }

        // Check if any service is degraded
        var degradedServices = services.Where(s => s!.IsDegraded).ToList();
        if (degradedServices.Any())
        {
            var names = string.Join(", ", degradedServices.Select(s => s!.ServiceName));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"JobScheduler services degraded: {names}"));
        }

        // Check if any service is not healthy (not Running or Executing)
        var unhealthyServices = services.Where(s => !s!.IsHealthy).ToList();
        if (unhealthyServices.Any())
        {
            var names = string.Join(", ", unhealthyServices.Select(s => $"{s!.ServiceName} ({s.CurrentState})"));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"JobScheduler services not fully initialized: {names}"));
        }

        // All services are healthy
        var healthyNames = string.Join(", ", services.Select(s => s!.ServiceName));
        return Task.FromResult(HealthCheckResult.Healthy(
            $"All JobScheduler services healthy: {healthyNames}"));
    }
}
