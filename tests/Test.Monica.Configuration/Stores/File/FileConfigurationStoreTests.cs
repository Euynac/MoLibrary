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
    public async Task EnsureCreatedAsync_WhenDocumentIsMissing_ShouldCreateDefinitionJson()
    {
        var store = CreateStore();
        var definition = TestConfigurationFactory.Definition();

        var document = await store.EnsureCreatedAsync(definition, """{"WorkerId":1}""", CancellationToken.None);

        document.Version.Should().Be(1);
        document.Json.Should().Contain("\"WorkerId\": 1");
        System.IO.File.Exists(Path.Combine(_rootDirectory, "effective", $"{definition.DefinitionKey}.json")).Should().BeTrue();
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
