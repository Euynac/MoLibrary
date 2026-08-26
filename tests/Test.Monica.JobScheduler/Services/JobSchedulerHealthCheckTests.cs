using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.JobScheduler.Services.Support;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobSchedulerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenAPlaneHasNotInitialized_ShouldBeUnhealthy()
    {
        var runtimeState = new JobSchedulerRuntimeState();
        var healthCheck = new JobSchedulerHealthCheck(runtimeState);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Scheduling has not initialized");
        result.Description.Should().Contain("Worker has not initialized");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenBothPlanesAreReady_ShouldBeHealthy()
    {
        var runtimeState = new JobSchedulerRuntimeState();
        _ = runtimeState.SetScheduling(true, "ready");
        _ = runtimeState.SetWorker(true, "ready");
        var healthCheck = new JobSchedulerHealthCheck(runtimeState);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSchedulingFails_ShouldReportItsMessage()
    {
        var runtimeState = new JobSchedulerRuntimeState();
        _ = runtimeState.SetWorker(true, "ready");
        _ = runtimeState.SetScheduling(false, "store unreachable");
        var healthCheck = new JobSchedulerHealthCheck(runtimeState);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("store unreachable");
    }
}
