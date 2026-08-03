using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Stores.File;
using Xunit;

namespace Test.Monica.Configuration.Stores.File;

public class FileConfigurationStoreTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"monica-config-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task EnsureCreatedAsync_WhenDocumentIsMissing_ShouldCreateEffectiveValueJson()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();

        var document = await store.EnsureCreatedAsync(definition, """{"WorkerId":1}""", CancellationToken.None);

        document.Version.Should().Be(1);
        document.Json.Should().Contain("\"WorkerId\": 1");
        System.IO.File.Exists(Path.Combine(_rootDirectory, "effective", $"{definition.DefinitionKey}.json")).Should().BeTrue();
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenDocumentExists_ShouldNotGenerateItsSeedAgain()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        await store.EnsureCreatedAsync(definition, """{"WorkerId":1}""", CancellationToken.None);
        var seedFactoryCalls = 0;
        var seed = new ConfigurationEffectiveValueSeed(
            definition,
            () =>
            {
                seedFactoryCalls++;
                return """{"WorkerId":2}""";
            });

        var documents = await store.EnsureCreatedAsync([seed], CancellationToken.None);

        seedFactoryCalls.Should().Be(0);
        documents.Should().ContainSingle();
        documents[0].Json.Should().Contain("\"WorkerId\": 1");
    }

    [Fact]
    public async Task SaveAsync_WhenExpectedVersionIsStale_ShouldThrowConcurrencyConflict()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        await store.EnsureCreatedAsync(definition, """{"WorkerId":1}""", CancellationToken.None);
        await store.SaveAsync(new ConfigurationEffectiveValueSaveRequest
        {
            Definition = definition,
            Json = """{"WorkerId":2}""",
            ExpectedVersion = 1
        }, CancellationToken.None);

        var act = () => store.SaveAsync(new ConfigurationEffectiveValueSaveRequest
        {
            Definition = definition,
            Json = """{"WorkerId":3}""",
            ExpectedVersion = 1
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationConcurrencyConflictException>();
    }

    [Fact]
    public async Task HistoryMethods_ShouldPersistGroupsAndHistoryRows()
    {
        var store = CreateStore();
        var group = new ConfigurationMutationGroup
        {
            GroupId = "group-1",
            Label = "Demo",
            CreatedTime = DateTimeOffset.UtcNow,
            DefinitionKeys = [TestConfigurationFactory.DefinitionKey],
            MutationCount = 1,
            Status = ConfigurationMutationGroupStatus.Applied
        };
        var history = new ConfigurationValueHistory
        {
            HistoryId = "history-1",
            DefinitionKey = TestConfigurationFactory.DefinitionKey,
            LogicalPath = LogicalPath.FromProperties("WorkerId"),
            MutationKind = ConfigurationMutationKind.Set,
            Granularity = ConfigurationMutationGranularity.Scalar,
            State = ConfigurationValueState.Active,
            NewValue = ConfigurationStoredValue.FromJson("2"),
            Version = 2,
            SchemaVersion = 1,
            ModifiedTime = DateTimeOffset.UtcNow,
            MutationGroupId = group.GroupId
        };

        await store.UpsertGroupAsync(group, CancellationToken.None);
        await store.AppendHistoryAsync(history, CancellationToken.None);

        (await store.GetGroupAsync(group.GroupId, CancellationToken.None)).Should().NotBeNull();
        (await store.QueryHistoryAsync(null, null, TestConfigurationFactory.DefinitionKey, null, group.GroupId, CancellationToken.None))
            .Should().ContainSingle(row => row.HistoryId == history.HistoryId);
    }

    [Fact]
    public async Task GetDefinitionPublicationOverviewAsync_WhenDefinitionChanges_ShouldExposeCurrentPublisherStateWithoutHistory()
    {
        var store = CreateStore();
        var publisher = new ConfigurationPublisherIdentity
        {
            PublisherKey = "Test.FileService",
            InstanceId = "test-file-service:1",
            Name = "Test File Service",
            Version = "1.0.0-test"
        };
        var original = TestConfigurationFactory.Definition() with
        {
            ReloadBehaviorObservationKind = ConfigurationReloadBehaviorObservationKind.Inferred,
            ReloadBehavior = ConfigurationReloadBehavior.OnlineReloadable
        };
        var stateOnlyChanged = original with
        {
            ReloadBehaviorObservationKind = ConfigurationReloadBehaviorObservationKind.Declared
        };
        var changed = stateOnlyChanged with { DisplayName = "Updated Test App" };

        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [original]),
            CancellationToken.None);
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [stateOnlyChanged]),
            CancellationToken.None);
        var stateOnlyOverview = await store.GetDefinitionPublicationOverviewAsync(
            original.DefinitionKey,
            20,
            CancellationToken.None);
        stateOnlyOverview.DefinitionRevision.Should().Be(1);
        stateOnlyOverview.PublisherStates.Should().ContainSingle();
        stateOnlyOverview.PublisherStates[0].ObservationKind
            .Should().Be(ConfigurationReloadBehaviorObservationKind.Declared);

        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(
                publisher with { InstanceId = "test-file-service:2" },
                [changed]),
            CancellationToken.None);

        var overview = await store.GetDefinitionPublicationOverviewAsync(
            original.DefinitionKey,
            20,
            CancellationToken.None);

        overview.DefinitionKey.Should().Be(original.DefinitionKey);
        overview.DefinitionRevision.Should().Be(2);
        overview.SchemaVersion.Should().Be(1);
        overview.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.OnlineReloadable);
        overview.PublisherStates.Should().ContainSingle();
        overview.PublisherStates[0].PublisherKey.Should().Be(publisher.PublisherKey);
        overview.PublisherStates[0].ObservationKind
            .Should().Be(ConfigurationReloadBehaviorObservationKind.Declared);
        overview.PublisherStates[0].ReloadBehavior
            .Should().Be(ConfigurationReloadBehavior.OnlineReloadable);
        overview.RevisionHistories.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefinitionPublisherStatesAsync_WhenDefinitionIsMissing_ShouldReturnCompleteCurrentSnapshot()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        var publisher = CreatePublisher("Test.FileImpact", "impact:1");
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [definition]),
            CancellationToken.None);

        var states = await store.GetDefinitionPublisherStatesAsync(
            [definition.DefinitionKey, "Test.FileImpact.Missing"],
            CancellationToken.None);

        states[definition.DefinitionKey].Should().ContainSingle(state =>
            state.PublisherKey == publisher.PublisherKey);
        states["Test.FileImpact.Missing"].Should().BeEmpty();
    }

    [Fact]
    public async Task PublisherLifecycle_WhenLastPublisherWithdrawsAndReturns_ShouldRetireThenReactivateDefinition()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        var publisher = CreatePublisher("Test.FileLifecycle", "instance:1");
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [definition]),
            CancellationToken.None);

        await store.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            CancellationToken.None);

        var retiredEntry = await store.GetPublishedDefinitionEntryAsync(
            definition.DefinitionKey,
            CancellationToken.None);
        var retiredOverview = await store.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            20,
            CancellationToken.None);
        retiredEntry.Should().NotBeNull();
        retiredEntry!.Metadata.LifecycleState.Should().Be(ConfigurationDefinitionLifecycleState.Retired);
        retiredOverview.LifecycleState.Should().Be(ConfigurationDefinitionLifecycleState.Retired);
        retiredOverview.PublisherStates.Should().BeEmpty();

        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(
                publisher with { InstanceId = "instance:2" },
                [definition]),
            CancellationToken.None);

        var reactivatedOverview = await store.GetDefinitionPublicationOverviewAsync(
            definition.DefinitionKey,
            20,
            CancellationToken.None);
        reactivatedOverview.LifecycleState.Should().Be(ConfigurationDefinitionLifecycleState.Active);
        reactivatedOverview.PublisherStates.Should().ContainSingle();
        reactivatedOverview.DefinitionRevision.Should().Be(retiredOverview.DefinitionRevision);
    }

    [Fact]
    public async Task PurgeDefinitionAsync_WhenDefinitionIsRetired_ShouldDeleteCurrentRecordsAndRetainAudit()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        var publisher = CreatePublisher("Test.FilePurge", "instance:1");
        var group = new ConfigurationMutationGroup
        {
            GroupId = "group-file-purge",
            Label = "File purge audit",
            DefinitionKeys = [definition.DefinitionKey],
            MutationCount = 1,
            CreatedTime = DateTimeOffset.UtcNow,
            Status = ConfigurationMutationGroupStatus.Applied
        };
        var history = new ConfigurationValueHistory
        {
            HistoryId = "history-file-purge",
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = LogicalPath.FromProperties("WorkerId"),
            MutationKind = ConfigurationMutationKind.Set,
            Granularity = ConfigurationMutationGranularity.Scalar,
            State = ConfigurationValueState.Active,
            NewValue = ConfigurationStoredValue.FromJson("1"),
            Version = 1,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            ModifiedTime = DateTimeOffset.UtcNow,
            MutationGroupId = group.GroupId
        };

        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [definition]),
            CancellationToken.None);
        var effectiveValue = await store.EnsureCreatedAsync(
            definition,
            """{"WorkerId":1}""",
            CancellationToken.None);
        await store.UpsertGroupAsync(group, CancellationToken.None);
        await store.AppendHistoryAsync(history, CancellationToken.None);
        var unifiedVersion = await store.AppendVersionAsync(
            new ConfigurationUnifiedVersionCreateRequest
            {
                MutationGroupId = group.GroupId,
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
            },
            CancellationToken.None);
        await store.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            CancellationToken.None);

        var preview = await store.PreviewDefinitionPurgeAsync(
            definition.DefinitionKey,
            CancellationToken.None);
        preview.CanPurge.Should().BeTrue();
        preview.HasEffectiveValue.Should().BeTrue();
        preview.PublicationHistoryCount.Should().Be(0);
        preview.RetainedValueHistoryCount.Should().Be(1);
        preview.RetainedMutationGroupCount.Should().Be(1);
        preview.RetainedUnifiedVersionCount.Should().Be(1);

        await store.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = preview.DefinitionRevision
            },
            CancellationToken.None);

        (await store.GetPublishedDefinitionEntryAsync(
            definition.DefinitionKey,
            CancellationToken.None)).Should().BeNull();
        (await store.GetAsync(
            definition.DefinitionKey,
            CancellationToken.None)).Should().BeNull();
        var retainedHistory = await store.GetHistoryByIdAsync(
            history.HistoryId,
            CancellationToken.None);
        retainedHistory.Should().NotBeNull();
        retainedHistory!.MutationGroupId.Should().Be(group.GroupId);
        var retainedGroup = await store.GetGroupAsync(
            group.GroupId,
            CancellationToken.None);
        retainedGroup.Should().NotBeNull();
        retainedGroup!.DefinitionKeys.Should().Contain(definition.DefinitionKey);
        var retainedSnapshot = await store.GetVersionAsync(
            unifiedVersion.Summary.Version,
            CancellationToken.None);
        retainedSnapshot.Should().NotBeNull();
        retainedSnapshot!.Definitions.Should().ContainSingle(document =>
            document.DefinitionKey == definition.DefinitionKey);
    }

    [Fact]
    public async Task PurgeDefinitionAsync_WhenDefinitionIsActive_ShouldRejectWithoutDeletingDefinition()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(
                CreatePublisher("Test.FileActivePurge", "instance:1"),
                [definition]),
            CancellationToken.None);

        var act = () => store.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = 1
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationConcurrencyConflictException>()
            .WithMessage("*active and cannot be purged*");
        (await store.GetPublishedDefinitionEntryAsync(
            definition.DefinitionKey,
            CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeDefinitionAsync_WhenReviewedRevisionIsStale_ShouldRejectWithoutDeletingDefinition()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        var publisher = CreatePublisher("Test.FileStalePurge", "instance:1");
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [definition]),
            CancellationToken.None);
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(
                publisher with { InstanceId = "instance:2" },
                [definition with { DisplayName = "Changed after preview" }]),
            CancellationToken.None);
        await store.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            CancellationToken.None);

        var act = () => store.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = 1
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationConcurrencyConflictException>()
            .WithMessage("*current revision is 2*");
        (await store.GetPublishedDefinitionEntryAsync(
            definition.DefinitionKey,
            CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeMaintenance_WhenDefinitionEnvelopeClaimsAnotherKey_ShouldRejectBeforeResolvingValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();
        var otherDefinition = definition with
        {
            DefinitionKey = "Test.OtherOptions",
            SectionPath = "Test:Other",
            DisplayName = "Test Other"
        };
        var publisher = CreatePublisher("Test.FileCorruptPurge", "instance:1");
        await store.PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, [definition, otherDefinition]),
            cancellationToken);
        await store.EnsureCreatedAsync(definition, """{"WorkerId":1}""", cancellationToken);
        await store.EnsureCreatedAsync(otherDefinition, """{"WorkerId":2}""", cancellationToken);
        await store.RetirePublisherAsync(
            publisher with { InstanceId = "instance:retire" },
            cancellationToken);

        var definitionPath = Path.Combine(
            _rootDirectory,
            "metadata",
            "definitions",
            $"{definition.DefinitionKey}.json");
        var envelope = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(definitionPath, cancellationToken))!.AsObject();
        envelope["DefinitionKey"] = otherDefinition.DefinitionKey;
        await System.IO.File.WriteAllTextAsync(definitionPath, envelope.ToJsonString(), cancellationToken);

        Func<Task> preview = async () =>
            _ = await store.PreviewDefinitionPurgeAsync(
                definition.DefinitionKey,
                cancellationToken);
        await preview.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*claims definition key*instead of*");

        var purge = () => store.PurgeDefinitionAsync(
            new ConfigurationDefinitionPurgeRequest
            {
                DefinitionKey = definition.DefinitionKey,
                ExpectedDefinitionRevision = 1
            },
            cancellationToken);
        await purge.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*claims definition key*instead of*");

        (await store.GetAsync(definition.DefinitionKey, cancellationToken)).Should().NotBeNull();
        (await store.GetAsync(otherDefinition.DefinitionKey, cancellationToken)).Should().NotBeNull();
        System.IO.File.Exists(definitionPath).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private FileConfigurationStore CreateStore()
    {
        return new FileConfigurationStore(Options.Create(new ConfigurationFileStoreOptions
        {
            RootDirectory = _rootDirectory
        }));
    }

    private static ConfigurationPublisherIdentity CreatePublisher(string publisherKey, string instanceId)
    {
        return new ConfigurationPublisherIdentity
        {
            PublisherKey = publisherKey,
            InstanceId = instanceId,
            Name = publisherKey,
            Version = "1.0.0-test"
        };
    }
}
