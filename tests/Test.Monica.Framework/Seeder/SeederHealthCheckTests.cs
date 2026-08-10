using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;
using Monica.Framework.Seeder.Services.Support;
using Monica.Modules;
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

    private static SeederState CreateState(params Type[] seederTypes)
    {
        return new SeederState(SeederGraph.Create(seederTypes, new ModuleSeederOption()), TimeProvider.System);
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
}
