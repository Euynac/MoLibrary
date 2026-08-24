using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobSchedulerHealthCheckTests
{
    [Theory]
    [InlineData(JobSchedulerRole.ControlPlane)]
    [InlineData(JobSchedulerRole.Worker)]
    [InlineData(JobSchedulerRole.Standalone)]
    public async Task CheckHealthAsync_WhenRequiredPlaneHasNotInitialized_ShouldBeUnhealthy(JobSchedulerRole role)
    {
        var healthCheck = CreateHealthCheck(role, out _);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenZeroJobWorkerRegisteredCapability_ShouldBeHealthy()
    {
        var healthCheck = CreateHealthCheck(JobSchedulerRole.Worker, out var runtimeState);
        runtimeState.SetWorker(true, "Worker capability is active for 0 local job(s).");

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStandalonePlanesAreReady_ShouldBeHealthy()
    {
        var healthCheck = CreateHealthCheck(JobSchedulerRole.Standalone, out var runtimeState);
        runtimeState.SetControlPlane(true, "Control plane initialized.");
        runtimeState.SetWorker(true, "Worker capability initialized.");

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    private static JobSchedulerHealthCheck CreateHealthCheck(
        JobSchedulerRole role,
        out JobSchedulerRuntimeState runtimeState)
    {
        runtimeState = new JobSchedulerRuntimeState();
        return new JobSchedulerHealthCheck(
            runtimeState,
            Options.Create(new ModuleJobSchedulerOption { Role = role }));
    }
}
