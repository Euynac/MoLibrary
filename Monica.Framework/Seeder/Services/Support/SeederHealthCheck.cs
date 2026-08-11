using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;

namespace Monica.Framework.Seeder.Services.Support;

internal sealed class SeederHealthCheck(ISeederState seederState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = seederState.GetSnapshot();
        var data = new Dictionary<string, object>
        {
            ["total"] = snapshot.Seeders.Length,
            ["requiredUnsuccessful"] = snapshot.RequiredUnsuccessfulCount,
            ["optionalUnsuccessful"] = snapshot.OptionalUnsuccessfulCount,
            ["completed"] = snapshot.IsCompleted,
            ["runStatus"] = snapshot.Status.ToString(),
            ["readinessStatus"] = snapshot.ReadinessStatus.ToString()
        };

        if (snapshot.FailFastTriggerSeederTypeName is not null)
        {
            data["failFastTrigger"] = snapshot.FailFastTriggerSeederTypeName;
        }

        if (snapshot.Status is SeederRunStatus.Aborting or SeederRunStatus.Aborted)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                snapshot.Status == SeederRunStatus.Aborting
                    ? "Seeder execution is aborting after a fail-fast failure."
                    : $"Seeder execution was aborted by '{snapshot.FailFastTriggerSeederTypeName}'.",
                data: data));
        }

        if (snapshot.ReadinessStatus == SeederReadinessStatus.Unhealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"{snapshot.RequiredUnsuccessfulCount} required seeders are pending, running, failed, blocked, or cancelled.",
                data: data));
        }

        if (snapshot.ReadinessStatus == SeederReadinessStatus.Degraded)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"{snapshot.OptionalUnsuccessfulCount} optional seeders failed, were blocked, or were cancelled.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            snapshot.IsCompleted
                ? "All required seeders completed successfully."
                : "No required seeder is preventing readiness.",
            data));
    }
}
