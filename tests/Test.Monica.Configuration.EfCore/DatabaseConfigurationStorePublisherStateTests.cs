using AwesomeAssertions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task PublishAsync_WhenLogicalPublishersReportDifferentBehaviors_ShouldPersistStatesAndUseConservativeAggregate()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var definition = CreateDefinition("Test.Publishers.Precedence");

        await PublishAsync(stores, "Service.Online", "online:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.OnlineReloadable));
        await PublishAsync(stores, "Service.Unresolved", "unresolved:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Unresolved,
            ConfigurationReloadBehavior.Unknown));
        await PublishAsync(stores, "Service.Restart", "restart:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Declared,
            ConfigurationReloadBehavior.RequiresRestart));
        await PublishAsync(stores, "Service.Static", "static:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Declared,
            ConfigurationReloadBehavior.StaticAfterStartup));
        await PublishAsync(stores, "Service.NotConsumed", "neutral:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.NotConsumed,
            ConfigurationReloadBehavior.Unknown));

        var overview = await GetOverviewAsync(stores, definition.DefinitionKey);

        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.StaticAfterStartup);
        overview.DefinitionRevision.Should().Be(4);
        overview.SchemaVersion.Should().Be(1);
        overview.PublisherStates.Should().HaveCount(5);
        overview.PublisherStates.Select(static state => state.PublisherKey)
            .Should().BeEquivalentTo(
            new[]
            {
                "Service.Online",
                "Service.Unresolved",
                "Service.Restart",
                "Service.Static",
                "Service.NotConsumed"
            });
        overview.PublisherStates.Single(state => state.PublisherKey == "Service.NotConsumed")
            .ObservationKind.Should().Be(ConfigurationReloadBehaviorObservationKind.NotConsumed);
        overview.RevisionHistories.Select(static history => history.DefinitionRevision)
            .Should().Equal(4, 3, 2, 1);
    }

    [Fact]
    public async Task PublishAsync_WhenReplicaWithSamePublisherKeyReplacesState_ShouldKeepSingleLogicalState()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var definition = CreateDefinition("Test.Publishers.ReplicaReplacement");

        await PublishAsync(stores, "Service.Replicated", "replica:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.OnlineReloadable));
        await PublishAsync(stores, "Service.Replicated", "replica:2", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.RequiresRestart));

        var overview = await GetOverviewAsync(stores, definition.DefinitionKey);

        overview.PublisherStates.Should().ContainSingle();
        overview.PublisherStates[0].PublisherKey.Should().Be("Service.Replicated");
        overview.PublisherStates[0].ReloadBehavior.Should().Be(ConfigurationReloadBehavior.RequiresRestart);
        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.RequiresRestart);
        overview.DefinitionRevision.Should().Be(2);
        overview.RevisionHistories.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishAsync_WhenPublisherStateChangesWithoutChangingAggregate_ShouldUpdateStateWithoutNewRevision()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var definition = CreateDefinition("Test.Publishers.StableAggregate");

        await PublishAsync(stores, "Service.Static", "static:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Declared,
            ConfigurationReloadBehavior.StaticAfterStartup));
        await PublishAsync(stores, "Service.Changing", "changing:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.OnlineReloadable));
        await PublishAsync(stores, "Service.Changing", "changing:2", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.RequiresRestart));

        var overview = await GetOverviewAsync(stores, definition.DefinitionKey);

        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.StaticAfterStartup);
        overview.DefinitionRevision.Should().Be(1);
        overview.RevisionHistories.Should().ContainSingle();
        overview.PublisherStates.Single(state => state.PublisherKey == "Service.Changing")
            .ReloadBehavior.Should().Be(ConfigurationReloadBehavior.RequiresRestart);
    }

    [Fact]
    public async Task PublishAsync_WhenCompleteBatchOmitsDefinition_ShouldPruneOnlyThatPublishersState()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var removed = CreateDefinition("Test.Publishers.Batch.Removed");
        var retained = CreateDefinition("Test.Publishers.Batch.Retained");
        var publisherA = CreatePublisher("Service.A", "service-a:1");

        await stores.Items[0].PublishAsync(
            CreatePublication(
                publisherA,
                [
                    WithObservation(
                        removed,
                        ConfigurationReloadBehaviorObservationKind.Declared,
                        ConfigurationReloadBehavior.StaticAfterStartup),
                    WithObservation(
                        retained,
                        ConfigurationReloadBehaviorObservationKind.Inferred,
                        ConfigurationReloadBehavior.OnlineReloadable)
                ]),
            TestContext.Current.CancellationToken);
        await PublishAsync(stores, "Service.B", "service-b:1", WithObservation(
            removed,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.OnlineReloadable));

        await stores.Items[0].PublishAsync(
            CreatePublication(
                publisherA,
                [WithObservation(
                    retained,
                    ConfigurationReloadBehaviorObservationKind.Inferred,
                    ConfigurationReloadBehavior.OnlineReloadable)]),
            TestContext.Current.CancellationToken);

        var removedOverview = await GetOverviewAsync(stores, removed.DefinitionKey);
        var retainedOverview = await GetOverviewAsync(stores, retained.DefinitionKey);

        removedOverview.PublisherStates.Should().ContainSingle();
        removedOverview.PublisherStates[0].PublisherKey.Should().Be("Service.B");
        removedOverview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.OnlineReloadable);
        removedOverview.DefinitionRevision.Should().Be(2);
        retainedOverview.PublisherStates.Should().ContainSingle();
        retainedOverview.PublisherStates[0].PublisherKey.Should().Be("Service.A");
        retainedOverview.DefinitionRevision.Should().Be(1);
    }

    [Fact]
    public async Task RetirePublisherAsync_WhenPublisherHasActiveState_ShouldRemoveItsStatesAndRecomputeDefinitions()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var definition = CreateDefinition("Test.Publishers.Retirement");
        var retiringPublisher = CreatePublisher("Service.Retiring", "retiring:1");

        await stores.Items[0].PublishAsync(
            CreatePublication(
                retiringPublisher,
                [WithObservation(
                    definition,
                    ConfigurationReloadBehaviorObservationKind.Declared,
                    ConfigurationReloadBehavior.RequiresRestart)]),
            TestContext.Current.CancellationToken);
        await PublishAsync(stores, "Service.Remaining", "remaining:1", WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.OnlineReloadable));

        await stores.Items[0].RetirePublisherAsync(
            retiringPublisher with { InstanceId = "retiring:retirement" },
            TestContext.Current.CancellationToken);

        var overview = await GetOverviewAsync(stores, definition.DefinitionKey);

        overview.PublisherStates.Should().ContainSingle();
        overview.PublisherStates[0].PublisherKey.Should().Be("Service.Remaining");
        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.OnlineReloadable);
        overview.DefinitionRevision.Should().Be(2);
        overview.RevisionHistories.Select(static history => history.DefinitionRevision)
            .Should().Equal(2, 1);
    }

    private static async Task PublishAsync(
        StoreSet stores,
        string publisherKey,
        string instanceId,
        ConfigurationDefinition definition)
    {
        await stores.Items[0].PublishAsync(
            CreatePublication(CreatePublisher(publisherKey, instanceId), [definition]),
            TestContext.Current.CancellationToken);
    }

    private static async Task<ConfigurationDefinitionPublicationOverview> GetOverviewAsync(
        StoreSet stores,
        string definitionKey)
    {
        return await stores.Items[0].GetDefinitionPublicationOverviewAsync(
            definitionKey,
            200,
            TestContext.Current.CancellationToken);
    }

    private static ConfigurationDefinition WithObservation(
        ConfigurationDefinition definition,
        ConfigurationReloadBehaviorObservationKind observationKind,
        ConfigurationReloadBehavior reloadBehavior)
    {
        return definition with
        {
            ReloadBehaviorObservationKind = observationKind,
            ReloadBehavior = reloadBehavior
        };
    }

    private static ConfigurationDefinitionPublicationBatch CreatePublication(
        ConfigurationPublisherIdentity publisher,
        IReadOnlyList<ConfigurationDefinition> definitions)
    {
        return ConfigurationDefinitionPublicationBatch.Create(publisher, definitions);
    }

    private static ConfigurationPublisherIdentity CreatePublisher(string publisherKey, string instanceId)
    {
        return new ConfigurationPublisherIdentity
        {
            PublisherKey = publisherKey,
            InstanceId = instanceId,
            Name = instanceId,
            Version = "1.0.0-test"
        };
    }
}
