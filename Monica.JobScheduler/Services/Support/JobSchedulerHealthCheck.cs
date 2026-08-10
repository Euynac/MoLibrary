using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Modules;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Health check for monitoring JobScheduler hosted services initialization status using the hosted service registry.
/// </summary>
internal sealed class JobSchedulerHealthCheck(
    IMoHostedServiceRegistry serviceRegistry,
    IOptions<ModuleJobSchedulerOption> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var expectedServices = GetExpectedServiceTypes(options.Value)
            .Select(serviceType => new ExpectedService(
                serviceType,
                serviceRegistry.GetServices(serviceType)))
            .ToArray();
        var missingServices = expectedServices
            .Where(static expected => expected.Instances.Count == 0)
            .Select(static expected => expected.ServiceType.Name)
            .ToArray();
        if (missingServices.Length > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "JobScheduler readiness cannot be established because expected hosted services are not registered: " +
                $"{string.Join(", ", missingServices)}."));
        }

        var services = expectedServices
            .SelectMany(static expected => expected.Instances)
            .ToArray();

        // Check if any service is faulted
        var faultedServices = services.Where(static service => service.IsFaulted).ToList();
        if (faultedServices.Any())
        {
            var errors = string.Join("; ", faultedServices.Select(s =>
            {
                var lastError = s.StateHistory
                    .Where(h => h.Exception != null)
                    .OrderByDescending(h => h.Timestamp)
                    .FirstOrDefault();
                return $"{s.ServiceName}: {lastError?.Message ?? "Unknown error"}";
            }));

            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"JobScheduler services failed: {errors}"));
        }

        // Check if any service is degraded
        var degradedServices = services.Where(static service => service.IsDegraded).ToList();
        if (degradedServices.Any())
        {
            var names = string.Join(", ", degradedServices.Select(static service => service.ServiceName));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"JobScheduler services degraded: {names}"));
        }

        // Check if any service is not healthy (not Running or Executing)
        var unhealthyServices = services.Where(static service => !service.IsHealthy).ToList();
        if (unhealthyServices.Any())
        {
            var names = string.Join(", ", unhealthyServices.Select(static service => $"{service.ServiceName} ({service.CurrentState})"));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"JobScheduler services not fully initialized: {names}"));
        }

        // All services are healthy
        var healthyNames = string.Join(", ", services.Select(static service => service.ServiceName));
        return Task.FromResult(HealthCheckResult.Healthy(
            $"All JobScheduler services healthy: {healthyNames}"));
    }

    private static IReadOnlyList<Type> GetExpectedServiceTypes(ModuleJobSchedulerOption options)
    {
        var expectedServices = new List<Type>
        {
            typeof(JobRegistrationHostedService),
            typeof(JobWorkerManagerHostedService)
        };

        if (!options.RunControlPlane)
        {
            return expectedServices;
        }

        expectedServices.Add(typeof(JobConcurrencyGuardHostedService));
        expectedServices.Add(typeof(JobSchedulerHostedService));

        if (options.EnableLongIntervalScheduler)
        {
            expectedServices.Add(typeof(LongIntervalSchedulerService));
        }

        if (options.EnableZombieDetection)
        {
            expectedServices.Add(typeof(JobZombieDetectorHostedService));
        }

        if (options.EnableHistoryCleanup)
        {
            expectedServices.Add(typeof(JobHistoryCleanupHostedService));
        }

        return expectedServices;
    }

    private sealed record ExpectedService(
        Type ServiceType,
        IReadOnlyList<HostedServiceRuntimeInfo> Instances);
}
