using Microsoft.Extensions.Diagnostics.HealthChecks;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.WorkerPlane;

namespace MoLibrary.JobScheduler.HealthChecks;

/// <summary>
/// Health check for monitoring JobScheduler hosted services initialization status
/// </summary>
public class JobSchedulerHealthCheck(
    JobConcurrencyGuardHostedService? concurrencyGuard = null,
    JobSchedulerHostedService? scheduler = null,
    JobRegistrationHostedService? registration = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var services = new List<(string Name, bool? IsInitialized, string? Error)>
        {
            ("JobConcurrencyGuard", concurrencyGuard?.IsInitialized, concurrencyGuard?.InitializationError),
            ("JobScheduler", scheduler?.IsInitialized, scheduler?.InitializationError),
            ("JobRegistration", registration?.IsInitialized, registration?.InitializationError)
        };

        // Filter out null services (services that are not registered)
        var activeServices = services.Where(s => s.IsInitialized.HasValue).ToList();

        if (activeServices.Count == 0)
        {
            // No services are registered, this is unusual but not necessarily unhealthy
            return Task.FromResult(HealthCheckResult.Healthy(
                "No JobScheduler services are registered"));
        }

        // Check if any service has an error
        var failedServices = activeServices.Where(s => !string.IsNullOrEmpty(s.Error)).ToList();
        if (failedServices.Any())
        {
            var errors = string.Join("; ", failedServices.Select(s => $"{s.Name}: {s.Error}"));
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"JobScheduler services initialization failed: {errors}"));
        }

        // Check if all services are initialized
        var uninitializedServices = activeServices.Where(s => s.IsInitialized == false).ToList();
        if (uninitializedServices.Any())
        {
            var names = string.Join(", ", uninitializedServices.Select(s => s.Name));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"JobScheduler services initializing: {names}"));
        }

        // All services are initialized successfully
        var initializedNames = string.Join(", ", activeServices.Select(s => s.Name));
        return Task.FromResult(HealthCheckResult.Healthy(
            $"All JobScheduler services initialized: {initializedNames}"));
    }
}
