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
}
