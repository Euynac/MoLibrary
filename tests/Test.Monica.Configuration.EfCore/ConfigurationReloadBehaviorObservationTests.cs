using AwesomeAssertions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed class ConfigurationReloadBehaviorObservationTests
{
    [Fact]
    public void Aggregate_WhenEveryEvidenceLevelExists_ShouldUseConservativePrecedence()
    {
        var online = ConfigurationReloadBehaviorObservation.Inferred(
            ConfigurationReloadBehavior.OnlineReloadable);
        var unresolved = ConfigurationReloadBehaviorObservation.Unresolved();
        var restart = ConfigurationReloadBehaviorObservation.Declared(
            ConfigurationReloadBehavior.RequiresRestart);
        var fixedAfterStartup = ConfigurationReloadBehaviorObservation.Declared(
            ConfigurationReloadBehavior.StaticAfterStartup);
        var notConsumed = ConfigurationReloadBehaviorObservation.NotConsumed();

        ConfigurationReloadBehaviorObservation.Aggregate([notConsumed, online])
            .Should().Be(ConfigurationReloadBehavior.OnlineReloadable);
        ConfigurationReloadBehaviorObservation.Aggregate([notConsumed, online, unresolved])
            .Should().Be(ConfigurationReloadBehavior.Unknown);
        ConfigurationReloadBehaviorObservation.Aggregate([notConsumed, online, unresolved, restart])
            .Should().Be(ConfigurationReloadBehavior.RequiresRestart);
        ConfigurationReloadBehaviorObservation.Aggregate([notConsumed, online, unresolved, restart, fixedAfterStartup])
            .Should().Be(ConfigurationReloadBehavior.StaticAfterStartup);
    }

    [Fact]
    public void Aggregate_WhenOnlyNotConsumedEvidenceExists_ShouldReturnUnknownWithoutParticipating()
    {
        var observation = ConfigurationReloadBehaviorObservation.NotConsumed();

        var result = ConfigurationReloadBehaviorObservation.Aggregate([observation]);

        observation.Participates.Should().BeFalse();
        result.Should().Be(ConfigurationReloadBehavior.Unknown);
    }
}
