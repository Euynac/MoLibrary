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
        var requiredUnsuccessful = snapshot.Seeders.Count(static seeder =>
            seeder.Criticality == SeederCriticality.Required && seeder.Status != SeederStatus.Succeeded);
        var optionalUnsuccessful = snapshot.Seeders.Count(static seeder =>
            seeder.Criticality == SeederCriticality.Optional &&
            seeder.Status is SeederStatus.Failed or SeederStatus.Blocked or SeederStatus.Cancelled);
        var data = new Dictionary<string, object>
        {
            ["total"] = snapshot.Seeders.Length,
            ["requiredUnsuccessful"] = requiredUnsuccessful,
            ["optionalUnsuccessful"] = optionalUnsuccessful,
            ["completed"] = snapshot.IsCompleted
        };

        if (requiredUnsuccessful > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"{requiredUnsuccessful} required seeders are pending, running, failed, blocked, or cancelled.",
                data: data));
        }

        if (optionalUnsuccessful > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"{optionalUnsuccessful} optional seeders failed, were blocked, or were cancelled.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            snapshot.IsCompleted
                ? "All required seeders completed successfully."
                : "No required seeder is preventing readiness.",
            data));
    }
}
