using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Services;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using NSubstitute;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobSchedulerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenNoSchedulerServicesAreRegistered_ShouldBeUnhealthy()
    {
        var healthCheck = CreateHealthCheck([], new ModuleJobSchedulerOption());

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("readiness cannot be established");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOneMandatoryServiceIsMissing_ShouldBeUnhealthy()
    {
        var healthCheck = CreateHealthCheck(
            [typeof(JobWorkerManagerHostedService)],
            new ModuleJobSchedulerOption());

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain(nameof(JobRegistrationHostedService));
        result.Description.Should().NotContain(nameof(JobWorkerManagerHostedService));
    }

    [Theory]
    [InlineData(EnabledControlPlaneService.LongIntervalScheduler)]
    [InlineData(EnabledControlPlaneService.ZombieDetector)]
    [InlineData(EnabledControlPlaneService.HistoryCleanup)]
    public async Task CheckHealthAsync_WhenEnabledControlPlaneServiceIsMissing_ShouldBeUnhealthy(
        EnabledControlPlaneService enabledService)
    {
        var options = new ModuleJobSchedulerOption
        {
            RunControlPlane = true,
            EnableLongIntervalScheduler = enabledService == EnabledControlPlaneService.LongIntervalScheduler,
            EnableZombieDetection = enabledService == EnabledControlPlaneService.ZombieDetector,
            EnableHistoryCleanup = enabledService == EnabledControlPlaneService.HistoryCleanup
        };
        var missingServiceType = enabledService switch
        {
            EnabledControlPlaneService.LongIntervalScheduler => typeof(LongIntervalSchedulerService),
            EnabledControlPlaneService.ZombieDetector => typeof(JobZombieDetectorHostedService),
            EnabledControlPlaneService.HistoryCleanup => typeof(JobHistoryCleanupHostedService),
            _ => throw new ArgumentOutOfRangeException(nameof(enabledService), enabledService, null)
        };
        var healthCheck = CreateHealthCheck(
            [
                typeof(JobRegistrationHostedService),
                typeof(JobWorkerManagerHostedService),
                typeof(JobConcurrencyGuardHostedService),
                typeof(JobSchedulerHostedService)
            ],
            options);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain(missingServiceType.Name);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenControlPlaneDoesNotRun_ShouldOnlyRequireWorkerServices()
    {
        var healthCheck = CreateHealthCheck(
            [typeof(JobRegistrationHostedService), typeof(JobWorkerManagerHostedService)],
            new ModuleJobSchedulerOption());

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOptionalControlPlaneServicesAreDisabled_ShouldNotRequireThem()
    {
        var options = new ModuleJobSchedulerOption
        {
            RunControlPlane = true,
            EnableLongIntervalScheduler = false,
            EnableZombieDetection = false,
            EnableHistoryCleanup = false
        };
        var healthCheck = CreateHealthCheck(
            [
                typeof(JobRegistrationHostedService),
                typeof(JobWorkerManagerHostedService),
                typeof(JobConcurrencyGuardHostedService),
                typeof(JobSchedulerHostedService)
            ],
            options);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    private static JobSchedulerHealthCheck CreateHealthCheck(
        IReadOnlyCollection<Type> registeredServiceTypes,
        ModuleJobSchedulerOption options)
    {
        var servicesByType = registeredServiceTypes.ToDictionary(
            static serviceType => serviceType,
            static serviceType => (IReadOnlyList<HostedServiceRuntimeInfo>)
            [CreateRuntimeInfo(serviceType)]);
        var registry = Substitute.For<IMoHostedServiceRegistry>();
        registry.GetServices(Arg.Any<Type>())
            .Returns(call => servicesByType.GetValueOrDefault(
                call.Arg<Type>(),
                Array.Empty<HostedServiceRuntimeInfo>()));

        return new JobSchedulerHealthCheck(registry, Options.Create(options));
    }

    private static HostedServiceRuntimeInfo CreateRuntimeInfo(Type serviceType)
    {
        var registry = new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption()));
        var tracker = registry.Register($"test-{serviceType.Name}", registration =>
        {
            registration.InstanceName = serviceType.Name;
            registration.InstanceType = serviceType;
        });
        tracker.RecordState("Healthy for test.", HostedServiceState.Running);
        return new HostedServiceRuntimeInfo(tracker);
    }

    public enum EnabledControlPlaneService
    {
        LongIntervalScheduler,
        ZombieDetector,
        HistoryCleanup
    }
}
