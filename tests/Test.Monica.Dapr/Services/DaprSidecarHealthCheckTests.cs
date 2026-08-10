using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.Dapr.Abstractions;
using Monica.Dapr.Models;
using Monica.Dapr.Services;
using NSubstitute;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprSidecarHealthCheckTests
{
    [Theory]
    [InlineData(DaprHealthStatus.Healthy, HealthStatus.Healthy)]
    [InlineData(DaprHealthStatus.Degraded, HealthStatus.Degraded)]
    [InlineData(DaprHealthStatus.NotStarted, HealthStatus.Unhealthy)]
    [InlineData(DaprHealthStatus.Checking, HealthStatus.Unhealthy)]
    [InlineData(DaprHealthStatus.Unhealthy, HealthStatus.Unhealthy)]
    [InlineData(DaprHealthStatus.Failed, HealthStatus.Unhealthy)]
    public async Task CheckHealthAsync_WhenCoordinatorHasState_ShouldProjectWithoutWaiting(
        DaprHealthStatus coordinatorStatus,
        HealthStatus expectedStatus)
    {
        var coordinator = Substitute.For<IDaprSidecarHealthCoordinator>();
        coordinator.Status.Returns(coordinatorStatus);
        var healthCheck = new DaprSidecarHealthCheck(coordinator);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, result.Status);
        await coordinator.DidNotReceiveWithAnyArgs()
            .WaitForHealthyAsync(default, TestContext.Current.CancellationToken);
    }
}
