using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.Models;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed class DatabaseConfigurationStorePublishTests
{
    private const string PUBLISH_DEFINITIONS_LOCK_MARKER_KEY = "Configuration.EfCore.PublishDefinitionsLock";

    [Fact]
    public async Task PublishAsync_WhenManyStoresPublishSameNewDefinition_ShouldCreateSingleDefinition()
    {
        var databasePath = CreateDatabasePath();
        await InitializeSchemaAsync(databasePath);

        var definition = CreateDefinition("Test.Concurrent.Shared", "sha256:shared");
        await using var stores = CreateStores(databasePath, 12);

        await Task.WhenAll(stores.Items.Select(store => store.PublishAsync([definition], TestContext.Current.CancellationToken)));

        var published = await stores.Items[0].ListPublishedDefinitionsAsync(TestContext.Current.CancellationToken);
        published.Where(candidate => candidate.DefinitionKey == definition.DefinitionKey)
            .Should().ContainSingle();
        var histories = await stores.Items[0].ListDefinitionPublishHistoriesAsync(
            definition.DefinitionKey,
            20,
            TestContext.Current.CancellationToken);
        histories.Should().ContainSingle();
        histories[0].ChangeKind.Should().Be(ConfigurationDefinitionPublishChangeKind.Created);
    }

    [Fact]
    public async Task PublishAsync_WhenLockRowIsMissingAndStoresPublishConcurrently_ShouldCreateSingleLockMarker()
    {
        var databasePath = CreateDatabasePath();
        await InitializeSchemaAsync(databasePath);

        var definition = CreateDefinition("Test.Concurrent.LockMarker", "sha256:lock-marker");
        await using var stores = CreateStores(databasePath, 12);

        await Task.WhenAll(stores.Items.Select(store => store.PublishAsync([definition], TestContext.Current.CancellationToken)));

        var lockMarkerCount = await CountPublishLockMarkersAsync(databasePath);
        lockMarkerCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_WhenManyStoresPublishSameBatch_ShouldPublishEveryDefinitionWithoutConcurrencyFailure()
    {
        var databasePath = CreateDatabasePath();
        await InitializeSchemaAsync(databasePath);

        var definitions = new[]
        {
            CreateDefinition("Test.Concurrent.Batch.One", "sha256:batch-one"),
            CreateDefinition("Test.Concurrent.Batch.Two", "sha256:batch-two"),
            CreateDefinition("Test.Concurrent.Batch.Three", "sha256:batch-three")
        };
        await using var stores = CreateStores(databasePath, 10);

        await Task.WhenAll(stores.Items.Select(store => store.PublishAsync(definitions, TestContext.Current.CancellationToken)));

        var published = await stores.Items[0].ListPublishedDefinitionsAsync(TestContext.Current.CancellationToken);
        published.Select(definition => definition.DefinitionKey)
            .Should().Contain(definitions.Select(static definition => definition.DefinitionKey));
        foreach (var definition in definitions)
        {
            var histories = await stores.Items[0].ListDefinitionPublishHistoriesAsync(
                definition.DefinitionKey,
                20,
                TestContext.Current.CancellationToken);
            histories.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task PublishAsync_WhenDefinitionAlreadyPublished_ShouldNoOpWithoutExtraHistory()
    {
        var databasePath = CreateDatabasePath();
        await using var stores = CreateStores(databasePath, 2);
        var definition = CreateDefinition("Test.Concurrent.NoOp", "sha256:no-op");

        await stores.Items[0].PublishAsync([definition], TestContext.Current.CancellationToken);
        await stores.Items[1].PublishAsync([definition], TestContext.Current.CancellationToken);

        var histories = await stores.Items[0].ListDefinitionPublishHistoriesAsync(
            definition.DefinitionKey,
            20,
            TestContext.Current.CancellationToken);
        histories.Should().ContainSingle();
        histories[0].ChangeKind.Should().Be(ConfigurationDefinitionPublishChangeKind.Created);
    }

    [Fact]
    public async Task PublishAsync_WhenMetadataChanges_ShouldKeepSchemaVersionAndRecordMetadataHistory()
    {
        var databasePath = CreateDatabasePath();
        await using var stores = CreateStores(databasePath, 1);
        var original = CreateDefinition("Test.Concurrent.Metadata", "sha256:metadata");
        var changed = original with { DisplayName = "Updated Metadata Display Name" };

        await stores.Items[0].PublishAsync([original], TestContext.Current.CancellationToken);
        await stores.Items[0].PublishAsync([changed], TestContext.Current.CancellationToken);

        var published = await stores.Items[0].GetPublishedDefinitionAsync(
            original.DefinitionKey,
            TestContext.Current.CancellationToken);
        published.Should().NotBeNull();
        published!.SchemaVersion.Should().Be(1);
        published.DisplayName.Should().Be(changed.DisplayName);

        var histories = await stores.Items[0].ListDefinitionPublishHistoriesAsync(
            original.DefinitionKey,
            20,
            TestContext.Current.CancellationToken);
        histories.Select(static history => history.ChangeKind)
            .Should().Contain([ConfigurationDefinitionPublishChangeKind.Created, ConfigurationDefinitionPublishChangeKind.MetadataChanged]);
    }

    [Fact]
    public async Task PublishAsync_WhenSchemaChanges_ShouldIncrementSchemaVersionAndRecordSchemaHistory()
    {
        var databasePath = CreateDatabasePath();
        await using var stores = CreateStores(databasePath, 1);
        var original = CreateDefinition("Test.Concurrent.Schema", "sha256:schema-v1");
        var changed = original with { SchemaHash = "sha256:schema-v2" };

        await stores.Items[0].PublishAsync([original], TestContext.Current.CancellationToken);
        await stores.Items[0].PublishAsync([changed], TestContext.Current.CancellationToken);

        var published = await stores.Items[0].GetPublishedDefinitionAsync(
            original.DefinitionKey,
            TestContext.Current.CancellationToken);
        published.Should().NotBeNull();
        published!.SchemaVersion.Should().Be(2);
        published.SchemaHash.Should().Be(changed.SchemaHash);

        var histories = await stores.Items[0].ListDefinitionPublishHistoriesAsync(
            original.DefinitionKey,
            20,
            TestContext.Current.CancellationToken);
        histories.Select(static history => history.ChangeKind)
            .Should().Contain([ConfigurationDefinitionPublishChangeKind.Created, ConfigurationDefinitionPublishChangeKind.SchemaChanged]);
    }

    private static async Task InitializeSchemaAsync(string databasePath)
    {
        await using var stores = CreateStores(databasePath, 1);
        _ = await stores.Items[0].ListPublishedDefinitionsAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountPublishLockMarkersAsync(string databasePath)
    {
        await using var provider = CreateProvider(databasePath);
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(*)
                              FROM "ConfigurationSchemaMarkers"
                              WHERE "MarkerKey" = $markerKey
                              """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$markerKey";
        parameter.Value = PUBLISH_DEFINITIONS_LOCK_MARKER_KEY;
        command.Parameters.Add(parameter);

        var count = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToInt32(count);
    }

    private static StoreSet CreateStores(string databasePath, int count)
    {
        var providers = Enumerable.Range(0, count)
            .Select(_ => CreateProvider(databasePath))
            .ToArray();
        return new StoreSet(
            providers,
            providers.Select(provider => provider.GetRequiredService<DatabaseConfigurationStore>()).ToArray());
    }

    private static ServiceProvider CreateProvider(string databasePath)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<ModuleConfigurationEfCoreOption>(static options => options.AutoCreateSchema = true);
        services.Configure<ModuleRepositoryOption>(static _ => { });
        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        services.AddSingleton<IAuditPropertySetter, NoOpAuditPropertySetter>();
        services.AddSingleton(
            typeof(IDbContextOperation<ConfigurationDbContext>),
            typeof(ScopedDbContextOperation<ConfigurationDbContext>));
        services.AddDbContext<ConfigurationDbContext>((_, options) =>
        {
            options.UseSqlite($"Data Source={databasePath}");
        });
        services.AddSingleton<DatabaseConfigurationStore>();
        return services.BuildServiceProvider();
    }

    private static ConfigurationDefinition CreateDefinition(string definitionKey, string schemaHash)
    {
        return new ConfigurationDefinition
        {
            DefinitionKey = definitionKey,
            SectionPath = definitionKey.Replace(".", ":", StringComparison.Ordinal),
            DisplayName = definitionKey,
            ClrTypeName = typeof(DatabaseConfigurationStorePublishTests).AssemblyQualifiedName!,
            FromProject = "Test.Project",
            Category = "Test",
            SchemaHash = schemaHash,
            Root = new ConfigurationNodeDefinition
            {
                NodeKey = string.Empty,
                Name = "Options",
                RelativePath = LogicalPath.Root,
                ConfigurationPath = definitionKey.Replace(".", ":", StringComparison.Ordinal),
                ClrTypeName = typeof(object).AssemblyQualifiedName!,
                NodeKind = ConfigurationNodeKind.Object,
                IsNullable = false,
                Children =
                [
                    new ConfigurationNodeDefinition
                    {
                        NodeKey = "Enabled",
                        Name = "Enabled",
                        RelativePath = LogicalPath.FromProperties("Enabled"),
                        ConfigurationPath = definitionKey.Replace(".", ":", StringComparison.Ordinal) + ":Enabled",
                        ClrTypeName = typeof(bool).AssemblyQualifiedName!,
                        NodeKind = ConfigurationNodeKind.Scalar,
                        ValueKind = ConfigurationValueKind.Boolean,
                        IsNullable = false
                    }
                ]
            }
        };
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "monica-configuration-efcore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "configuration.db");
    }

    private sealed class StoreSet(ServiceProvider[] providers, DatabaseConfigurationStore[] items) : IAsyncDisposable
    {
        public DatabaseConfigurationStore[] Items { get; } = items;

        public async ValueTask DisposeAsync()
        {
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }

    private sealed class NoOpAuditPropertySetter : IAuditPropertySetter
    {
        public void SetCreationProperties(object targetObject)
        {
        }

        public void SetModificationProperties(object targetObject)
        {
        }

        public void SetDeletionProperties(object targetObject)
        {
        }

        public void IncrementEntityVersionProperty(object targetObject)
        {
        }
    }
}
