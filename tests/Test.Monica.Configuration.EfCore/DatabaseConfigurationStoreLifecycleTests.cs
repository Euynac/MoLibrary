using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task PublisherLifecycle_WhenLastPublisherWithdrawsAndReturns_ShouldRetireThenReactivateDefinition()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var provider = CreateProvider(databasePath);
        var metadataStore = provider.GetRequiredService<IConfigurationMetadataStore>();
        var definition = CreateDefinition("Test.Lifecycle.Reactivation");
        var publisher = CreatePublisher("Test.Lifecycle.Service", "instance:1");

        await metadataStore.PublishAsync(
            CreatePublication(publisher, [definition]),
            TestContext.Current.CancellationToken);
        await metadataStore.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            TestContext.Current.CancellationToken);

        var retiredEntry = await metadataStore.GetPublishedDefinitionEntryAsync(
            definition.DefinitionKey,
            TestContext.Current.CancellationToken);
        var retiredOverview = await metadataStore.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            20,
            TestContext.Current.CancellationToken);
        retiredEntry.Should().NotBeNull();
        retiredEntry!.Metadata.LifecycleState.Should().Be(ConfigurationDefinitionLifecycleState.Retired);
        retiredOverview.LifecycleState.Should().Be(ConfigurationDefinitionLifecycleState.Retired);
        retiredOverview.PublisherStates.Should().BeEmpty();

        await metadataStore.PublishAsync(
            CreatePublication(publisher with { InstanceId = "instance:2" }, [definition]),
            TestContext.Current.CancellationToken);

        var reactivatedOverview = await metadataStore.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            20,
            TestContext.Current.CancellationToken);
        reactivatedOverview.LifecycleState.Should().Be(ConfigurationDefinitionLifecycleState.Active);
        reactivatedOverview.PublisherStates.Should().ContainSingle();
        reactivatedOverview.DefinitionRevision.Should().Be(retiredOverview.DefinitionRevision);
        reactivatedOverview.RevisionHistories.Should().HaveCount(retiredOverview.RevisionHistories.Count);
    }

    [Fact]
    public async Task PurgeDefinitionAsync_WhenDefinitionHasPublisher_ShouldRejectWithoutDeletingDefinition()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var provider = CreateProvider(databasePath);
        var metadataStore = provider.GetRequiredService<IConfigurationMetadataStore>();
        var maintenanceStore = provider.GetRequiredService<IConfigurationDefinitionMaintenanceStore>();
        var definition = CreateDefinition("Test.Lifecycle.ActivePurge");
        await metadataStore.PublishAsync(
            CreatePublication([definition]),
            TestContext.Current.CancellationToken);

        var act = () => maintenanceStore.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = 1
            },
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConfigurationConcurrencyConflictException>()
            .WithMessage("*active and cannot be purged*");
        (await metadataStore.GetPublishedDefinitionEntryAsync(
                definition.DefinitionKey,
                TestContext.Current.CancellationToken))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeDefinitionAsync_WhenReviewedRevisionIsStale_ShouldRejectWithoutDeletingDefinition()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var provider = CreateProvider(databasePath);
        var metadataStore = provider.GetRequiredService<IConfigurationMetadataStore>();
        var maintenanceStore = provider.GetRequiredService<IConfigurationDefinitionMaintenanceStore>();
        var definition = CreateDefinition("Test.Lifecycle.StalePurge");
        var publisher = CreatePublisher("Test.Lifecycle.StaleService", "instance:1");
        await metadataStore.PublishAsync(
            CreatePublication(publisher, [definition]),
            TestContext.Current.CancellationToken);
        await metadataStore.PublishAsync(
            CreatePublication(
                publisher with { InstanceId = "instance:2" },
                [definition with { DisplayName = "Changed after preview" }]),
            TestContext.Current.CancellationToken);
        await metadataStore.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            TestContext.Current.CancellationToken);

        var act = () => maintenanceStore.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = 1
            },
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConfigurationConcurrencyConflictException>()
            .WithMessage("*current revision is 2*");
        (await metadataStore.GetPublishedDefinitionEntryAsync(
                definition.DefinitionKey,
                TestContext.Current.CancellationToken))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeDefinitionAsync_WhenDefinitionIsRetired_ShouldDeleteMutableRecordsAndRetainAudit()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var provider = CreateProvider(databasePath);
        var metadataStore = provider.GetRequiredService<IConfigurationMetadataStore>();
        var maintenanceStore = provider.GetRequiredService<IConfigurationDefinitionMaintenanceStore>();
        var effectiveValueStore = provider.GetRequiredService<IConfigurationEffectiveValueStore>();
        var historyStore = provider.GetRequiredService<IConfigurationHistoryStore>();
        var unifiedVersionStore = provider.GetRequiredService<IConfigurationUnifiedVersionStore>();
        var definition = CreateDefinition("Test.Lifecycle.PurgeAudit");
        var publisher = CreatePublisher("Test.Lifecycle.PurgeService", "instance:1");
        var group = CreateAuditGroup(definition.DefinitionKey);
        var history = CreateAuditHistory(definition, group.GroupId);

        await metadataStore.PublishAsync(
            CreatePublication(publisher, [definition]),
            TestContext.Current.CancellationToken);
        var effectiveValue = await effectiveValueStore.EnsureCreatedAsync(
            definition,
            """{"Enabled":true}""",
            TestContext.Current.CancellationToken);
        await historyStore.UpsertGroupAsync(group, TestContext.Current.CancellationToken);
        await historyStore.AppendHistoryAsync(history, TestContext.Current.CancellationToken);
        var unifiedVersion = await unifiedVersionStore.AppendVersionAsync(
            CreateUnifiedVersion(definition, effectiveValue, group.GroupId),
            TestContext.Current.CancellationToken);
        await metadataStore.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            TestContext.Current.CancellationToken);

        var preview = await maintenanceStore.PreviewDefinitionPurgeAsync(
            definition.DefinitionKey,
            TestContext.Current.CancellationToken);
        preview.CanPurge.Should().BeTrue();
        preview.HasEffectiveValue.Should().BeTrue();
        preview.PublicationHistoryCount.Should().Be(1);
        preview.RetainedValueHistoryCount.Should().Be(1);
        preview.RetainedMutationGroupCount.Should().Be(1);
        preview.RetainedUnifiedVersionCount.Should().Be(1);

        await maintenanceStore.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = preview.DefinitionRevision
            },
            TestContext.Current.CancellationToken);

        (await metadataStore.GetPublishedDefinitionEntryAsync(
            definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().BeNull();
        (await effectiveValueStore.GetAsync(
            definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().BeNull();
        var retainedHistory = await historyStore.GetHistoryByIdAsync(
            history.HistoryId,
            TestContext.Current.CancellationToken);
        retainedHistory.Should().NotBeNull();
        retainedHistory!.MutationGroupId.Should().Be(group.GroupId);
        var retainedGroup = await historyStore.GetGroupAsync(
            group.GroupId,
            TestContext.Current.CancellationToken);
        retainedGroup.Should().NotBeNull();
        retainedGroup!.DefinitionKeys.Should().Contain(definition.DefinitionKey);
        var retainedSnapshot = await unifiedVersionStore.GetVersionAsync(
            unifiedVersion.Summary.Version,
            TestContext.Current.CancellationToken);
        retainedSnapshot.Should().NotBeNull();
        retainedSnapshot!.Definitions.Should().ContainSingle(document =>
            document.DefinitionKey == definition.DefinitionKey);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        (await dbContext.ConfigurationDefinitions.CountAsync(
            row => row.DefinitionKey == definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().Be(0);
        (await dbContext.ConfigurationDefinitionPublishHistories.CountAsync(
            row => row.DefinitionKey == definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().Be(0);
        (await dbContext.ConfigurationEffectiveValues.CountAsync(
            row => row.DefinitionKey == definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().Be(0);
        (await dbContext.ConfigurationValueHistories.CountAsync(
            row => row.DefinitionKey == definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().Be(1);
        (await dbContext.ConfigurationUnifiedVersionDocuments.CountAsync(
            row => row.DefinitionKey == definition.DefinitionKey,
            TestContext.Current.CancellationToken)).Should().Be(1);
    }

    private static ConfigurationMutationGroup CreateAuditGroup(string definitionKey)
    {
        return new ConfigurationMutationGroup
        {
            GroupId = $"group-{Guid.NewGuid():N}",
            Label = "Lifecycle audit",
            DefinitionKeys = [definitionKey],
            MutationCount = 1,
            CreatedTime = DateTimeOffset.UtcNow,
            Status = ConfigurationMutationGroupStatus.Applied
        };
    }

    private static ConfigurationValueHistory CreateAuditHistory(
        ConfigurationDefinition definition,
        string groupId)
    {
        return new ConfigurationValueHistory
        {
            HistoryId = $"history-{Guid.NewGuid():N}",
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = LogicalPath.FromProperties("Enabled"),
            MutationKind = ConfigurationMutationKind.Set,
            Granularity = ConfigurationMutationGranularity.Scalar,
            State = ConfigurationValueState.Active,
            NewValue = ConfigurationStoredValue.FromJson("true"),
            Version = 1,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            ModifiedTime = DateTimeOffset.UtcNow,
            MutationGroupId = groupId
        };
    }

    private static ConfigurationUnifiedVersionCreateRequest CreateUnifiedVersion(
        ConfigurationDefinition definition,
        ConfigurationEffectiveValueDocument effectiveValue,
        string groupId)
    {
        return new ConfigurationUnifiedVersionCreateRequest
        {
            MutationGroupId = groupId,
            TriggerDefinitionKeys = [definition.DefinitionKey],
            Definitions =
            [
                new ConfigurationUnifiedVersionDefinitionSnapshot
                {
                    DefinitionKey = definition.DefinitionKey,
                    DisplayName = definition.DisplayName,
                    Category = definition.Category,
                    FromProject = definition.FromProject,
                    SchemaVersion = definition.SchemaVersion,
                    SchemaHash = definition.SchemaHash,
                    EffectiveValueVersion = effectiveValue.Version,
                    Json = effectiveValue.Json
                }
            ]
        };
    }
}
