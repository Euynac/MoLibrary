using System.Collections.Concurrent;
using System.Data.Common;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task PublishAsync_WhenSnapshotIsExact_ShouldSkipCrossPublisherReconciliation()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var interceptor = new MetadataQueryInterceptor();
        await using var provider = CreateProvider(databasePath, interceptor);
        var store = provider.GetRequiredService<IConfigurationMetadataStore>();
        var definition = CreateDefinition("Test.Publishers.FastPath.Exact");

        await store.PublishAsync(
            CreatePublication(CreatePublisher("Test.Service", "replica:1"), [definition]),
            TestContext.Current.CancellationToken);
        interceptor.Clear();

        await store.PublishAsync(
            CreatePublication(CreatePublisher("Test.Service", "replica:2"), [definition]),
            TestContext.Current.CancellationToken);

        interceptor.PublisherStateQueryCount.Should().Be(1);
        var overview = await store.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            200,
            TestContext.Current.CancellationToken);
        overview.DefinitionRevision.Should().Be(1);
        overview.PublisherStates.Should().ContainSingle();
        overview.RevisionHistories.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_WhenCanonicalMetadataChanges_ShouldRunCrossPublisherReconciliation()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var interceptor = new MetadataQueryInterceptor();
        await using var provider = CreateProvider(databasePath, interceptor);
        var store = provider.GetRequiredService<IConfigurationMetadataStore>();
        var definition = CreateDefinition("Test.Publishers.FastPath.Metadata");
        var changed = definition with { DisplayName = "Changed display name" };

        await store.PublishAsync(
            CreatePublication(CreatePublisher("Test.Service", "replica:1"), [definition]),
            TestContext.Current.CancellationToken);
        interceptor.Clear();

        await store.PublishAsync(
            CreatePublication(CreatePublisher("Test.Service", "replica:2"), [changed]),
            TestContext.Current.CancellationToken);

        interceptor.PublisherStateQueryCount.Should().Be(2);
        var overview = await store.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            200,
            TestContext.Current.CancellationToken);
        overview.DefinitionRevision.Should().Be(2);
        overview.RevisionHistories.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishAsync_WhenOnePublisherRepeatsExactSnapshot_ShouldPreserveOtherPublisherState()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var interceptor = new MetadataQueryInterceptor();
        await using var provider = CreateProvider(databasePath, interceptor);
        var store = provider.GetRequiredService<IConfigurationMetadataStore>();
        var definition = CreateDefinition("Test.Publishers.FastPath.Shared");
        var online = WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Inferred,
            ConfigurationReloadBehavior.OnlineReloadable);
        var fixedAfterStartup = WithObservation(
            definition,
            ConfigurationReloadBehaviorObservationKind.Declared,
            ConfigurationReloadBehavior.StaticAfterStartup);

        await store.PublishAsync(
            CreatePublication(CreatePublisher("Service.Online", "online:1"), [online]),
            TestContext.Current.CancellationToken);
        await store.PublishAsync(
            CreatePublication(CreatePublisher("Service.Static", "static:1"), [fixedAfterStartup]),
            TestContext.Current.CancellationToken);
        interceptor.Clear();

        await store.PublishAsync(
            CreatePublication(CreatePublisher("Service.Online", "online:2"), [online]),
            TestContext.Current.CancellationToken);

        interceptor.PublisherStateQueryCount.Should().Be(1);
        var overview = await store.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            200,
            TestContext.Current.CancellationToken);
        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.StaticAfterStartup);
        overview.PublisherStates.Should().HaveCount(2);
        overview.PublisherStates.Select(static state => state.PublisherKey)
            .Should().BeEquivalentTo(["Service.Online", "Service.Static"]);
        overview.DefinitionRevision.Should().Be(2);
        overview.RevisionHistories.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishAsync_WhenPublisherObservationChanges_ShouldRunCrossPublisherReconciliation()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var interceptor = new MetadataQueryInterceptor();
        await using var provider = CreateProvider(databasePath, interceptor);
        var store = provider.GetRequiredService<IConfigurationMetadataStore>();
        var definition = CreateDefinition("Test.Publishers.FastPath.Observation");
        var publisher = CreatePublisher("Service.Changing", "changing:1");

        await store.PublishAsync(
            CreatePublication(
                CreatePublisher("Service.Static", "static:1"),
                [WithObservation(
                    definition,
                    ConfigurationReloadBehaviorObservationKind.Declared,
                    ConfigurationReloadBehavior.StaticAfterStartup)]),
            TestContext.Current.CancellationToken);
        await store.PublishAsync(
            CreatePublication(
                publisher,
                [WithObservation(
                    definition,
                    ConfigurationReloadBehaviorObservationKind.Inferred,
                    ConfigurationReloadBehavior.OnlineReloadable)]),
            TestContext.Current.CancellationToken);
        interceptor.Clear();

        await store.PublishAsync(
            CreatePublication(
                publisher with { InstanceId = "changing:2" },
                [WithObservation(
                    definition,
                    ConfigurationReloadBehaviorObservationKind.Inferred,
                    ConfigurationReloadBehavior.RequiresRestart)]),
            TestContext.Current.CancellationToken);

        interceptor.PublisherStateQueryCount.Should().Be(2);
        var overview = await store.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            200,
            TestContext.Current.CancellationToken);
        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.StaticAfterStartup);
        overview.PublisherStates.Single(state => state.PublisherKey == publisher.PublisherKey)
            .ReloadBehavior.Should().Be(ConfigurationReloadBehavior.RequiresRestart);
    }

    [Fact]
    public async Task PublishAsync_WhenCompleteSnapshotOmitsDefinition_ShouldRunCrossPublisherReconciliation()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var interceptor = new MetadataQueryInterceptor();
        await using var provider = CreateProvider(databasePath, interceptor);
        var store = provider.GetRequiredService<IConfigurationMetadataStore>();
        var retained = CreateDefinition("Test.Publishers.FastPath.Retained");
        var removed = CreateDefinition("Test.Publishers.FastPath.Removed");
        var publisher = CreatePublisher("Service.Pruning", "pruning:1");

        await store.PublishAsync(
            CreatePublication(publisher, [retained, removed]),
            TestContext.Current.CancellationToken);
        interceptor.Clear();

        await store.PublishAsync(
            CreatePublication(publisher with { InstanceId = "pruning:2" }, [retained]),
            TestContext.Current.CancellationToken);

        interceptor.PublisherStateQueryCount.Should().BeGreaterThan(1);
        var removedOverview = await store.GetDefinitionPublicationOverviewAsync(
            removed.DefinitionKey,
            200,
            TestContext.Current.CancellationToken);
        removedOverview.PublisherStates.Should().BeEmpty();
    }

    private sealed class MetadataQueryInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();

        internal int PublisherStateQueryCount => _commands.Count(static command =>
            command.Contains(
                "FROM \"ConfigurationDefinitionPublisherStates\"",
                StringComparison.Ordinal));

        internal void Clear()
        {
            _commands.Clear();
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _commands.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
