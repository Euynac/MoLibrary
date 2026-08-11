using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;
using Monica.Framework.Seeder.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;
using Xunit;

namespace Test.Monica.Framework.Seeder;

public sealed class SeederHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRequiredSeederIsPending_ShouldBeUnhealthy()
    {
        var state = CreateState(typeof(RequiredSeeder));
        var check = new SeederHealthCheck(state);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOnlyOptionalSeederFails_ShouldBeDegraded()
    {
        var state = CreateState(typeof(RequiredSeeder), typeof(OptionalSeeder));
        state.MarkRunning(typeof(RequiredSeeder), 1);
        state.MarkSucceeded(typeof(RequiredSeeder));
        state.MarkRunning(typeof(OptionalSeeder), 1);
        state.MarkFailed(typeof(OptionalSeeder), new InvalidOperationException("Expected failure."));
        state.MarkSchedulerCompleted();
        var check = new SeederHealthCheck(state);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllRequiredSeedersSucceed_ShouldBeHealthy()
    {
        var state = CreateState(typeof(RequiredSeeder));
        state.MarkRunning(typeof(RequiredSeeder), 1);
        state.MarkSucceeded(typeof(RequiredSeeder));
        state.MarkSchedulerCompleted();
        var check = new SeederHealthCheck(state);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOptionalFailFastSeederIsAborting_ShouldBeUnhealthyWithTriggerData()
    {
        var state = CreateOptionalFailFastState(aborted: false);
        var check = new SeederHealthCheck(state);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["requiredUnsuccessful"].Should().Be(0);
        result.Data["optionalUnsuccessful"].Should().Be(1);
        result.Data["runStatus"].Should().Be(nameof(SeederRunStatus.Aborting));
        result.Data["failFastTrigger"].Should().Be(typeof(OptionalFailFastSeeder).GetCleanFullName());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOptionalFailFastSeederIsAborted_ShouldBeUnhealthyWithTriggerData()
    {
        var state = CreateOptionalFailFastState(aborted: true);
        var check = new SeederHealthCheck(state);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["requiredUnsuccessful"].Should().Be(0);
        result.Data["optionalUnsuccessful"].Should().Be(1);
        result.Data["runStatus"].Should().Be(nameof(SeederRunStatus.Aborted));
        result.Data["failFastTrigger"].Should().Be(typeof(OptionalFailFastSeeder).GetCleanFullName());
    }

    private static SeederState CreateState(params Type[] seederTypes)
    {
        return new SeederState(SeederGraph.Create(seederTypes, new ModuleSeederOption()), TimeProvider.System);
    }

    private static SeederState CreateOptionalFailFastState(bool aborted)
    {
        var state = CreateState(typeof(OptionalFailFastSeeder));
        var exception = new InvalidOperationException("Expected fail-fast failure.");
        state.MarkSchedulerStarted();
        state.MarkRunning(typeof(OptionalFailFastSeeder), 1);
        state.MarkFailFastTriggered(typeof(OptionalFailFastSeeder), exception);
        if (aborted)
        {
            state.MarkSchedulerAborted();
        }

        return state;
    }

    private sealed class RequiredSeeder : global::Monica.Framework.Seeder.Abstractions.ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [global::Monica.Framework.Seeder.Annotations.SeederPolicy(Criticality = SeederCriticality.Optional)]
    private sealed class OptionalSeeder : global::Monica.Framework.Seeder.Abstractions.ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [global::Monica.Framework.Seeder.Annotations.SeederPolicy(
        Criticality = SeederCriticality.Optional,
        FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class OptionalFailFastSeeder : global::Monica.Framework.Seeder.Abstractions.ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
