using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Reports whether the local scheduler responsibilities have reached a usable state.
/// </summary>
internal sealed class JobSchedulerHealthCheck(
    JobSchedulerRuntimeState runtimeState,
    IOptions<ModuleJobSchedulerOption> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = options.Value.Role;
        var controlReady = role == JobSchedulerRole.Worker || runtimeState.ControlPlaneReady;
        var workerReady = role == JobSchedulerRole.ControlPlane || runtimeState.WorkerReady;
        if (controlReady && workerReady)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"JobScheduler {role} responsibilities are ready."));
        }

        var reasons = new[]
            {
                controlReady ? null : runtimeState.ControlPlaneMessage ?? "Control plane has not initialized.",
                workerReady ? null : runtimeState.WorkerMessage ?? "Worker has not initialized."
            }
            .Where(static reason => reason is not null);
        return Task.FromResult(HealthCheckResult.Unhealthy(string.Join(" ", reasons)));
    }
}
