using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Bootstrap;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Monica.Core.Modularity.Extensions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    private const string DEFINITION_PUBLICATION_LOCK_KEY = "DefinitionPublication";
    private const string MUTATION_GROUPS_LOCK_KEY = "MutationGroups";
    private const string UNIFIED_VERSIONS_LOCK_KEY = "UnifiedVersions";

    [Fact]
    public async Task PublishAsync_WhenManyStoresPublishSameNewDefinition_ShouldCreateSingleDefinition()
    {
        var databasePath = await CreateMigratedDatabaseAsync();

        var definition = CreateDefinition("Test.Concurrent.Shared");
        await using var stores = CreateStores(databasePath, 12);

        await Task.WhenAll(stores.Items.Select(store => store.PublishAsync(
            CreatePublication([definition]),
            TestContext.Current.CancellationToken)));

        var published = (await stores.Items[0].ListPublishedDefinitionEntriesAsync(TestContext.Current.CancellationToken))
            .Select(static entry => entry.RequireDefinition())
            .ToArray();
        published.Where(candidate => candidate.DefinitionKey == definition.DefinitionKey)
            .Should().ContainSingle();
        var overview = await GetOverviewAsync(stores, definition.DefinitionKey);
        overview.DefinitionRevision.Should().Be(1);
        overview.RevisionHistories.Should().ContainSingle();
        overview.RevisionHistories[0].ChangeKind.Should().Be(ConfigurationDefinitionPublishChangeKind.Created);
    }

    [Fact]
    public async Task ApplyMigrationsAsync_WhenDatabaseIsEmpty_ShouldSeedEveryStoreLock()
    {
        var databasePath = await CreateMigratedDatabaseAsync();

        var locks = await ReadStoreLocksAsync(databasePath);
        locks.Should().BeEquivalentTo(
        [
            new StoreLockRow(DEFINITION_PUBLICATION_LOCK_KEY, 0),
            new StoreLockRow(MUTATION_GROUPS_LOCK_KEY, 0),
            new StoreLockRow(UNIFIED_VERSIONS_LOCK_KEY, 0)
        ]);
        var legacyMarkerTableCount = await CountConfigurationTablesAsync(
            databasePath,
            "ConfigurationSchemaMarkers");
        legacyMarkerTableCount.Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_WhenDefinitionPublicationLockIsMissing_ShouldRejectIncompatibleSchema()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await DeleteStoreLockAsync(databasePath, DEFINITION_PUBLICATION_LOCK_KEY);
        await using var stores = CreateStores(databasePath, 1);
        var definition = CreateDefinition("Test.Concurrent.MissingLock");

        var act = () => stores.Items[0].PublishAsync(
            CreatePublication([definition]),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConfigurationStoreSchemaException>()
            .WithMessage("*EF Core migration*");
    }

    [Fact]
    public async Task PublishAsync_WhenManyStoresPublishSameBatch_ShouldPublishEveryDefinitionWithoutConcurrencyFailure()
    {
        var databasePath = await CreateMigratedDatabaseAsync();

        var definitions = new[]
        {
            CreateDefinition("Test.Concurrent.Batch.One"),
            CreateDefinition("Test.Concurrent.Batch.Two"),
            CreateDefinition("Test.Concurrent.Batch.Three")
        };
        await using var stores = CreateStores(databasePath, 10);

        await Task.WhenAll(stores.Items.Select(store => store.PublishAsync(
            CreatePublication(definitions),
            TestContext.Current.CancellationToken)));

        var published = (await stores.Items[0].ListPublishedDefinitionEntriesAsync(TestContext.Current.CancellationToken))
            .Select(static entry => entry.RequireDefinition())
            .ToArray();
        published.Select(definition => definition.DefinitionKey)
            .Should().Contain(definitions.Select(static definition => definition.DefinitionKey));
        foreach (var definition in definitions)
        {
            var overview = await GetOverviewAsync(stores, definition.DefinitionKey);
            overview.RevisionHistories.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task PublishAsync_WhenDefinitionAlreadyPublished_ShouldNoOpWithoutExtraHistory()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 2);
        var definition = CreateDefinition("Test.Concurrent.NoOp");

        await stores.Items[0].PublishAsync(CreatePublication([definition]), TestContext.Current.CancellationToken);
        await stores.Items[1].PublishAsync(CreatePublication([definition]), TestContext.Current.CancellationToken);

        var overview = await GetOverviewAsync(stores, definition.DefinitionKey);
        overview.DefinitionRevision.Should().Be(1);
        overview.SchemaVersion.Should().Be(1);
        overview.PublisherStates.Should().ContainSingle();
        overview.RevisionHistories.Should().ContainSingle();
        overview.RevisionHistories[0].ChangeKind.Should().Be(ConfigurationDefinitionPublishChangeKind.Created);
    }

    [Fact]
    public async Task PublishAsync_WhenMetadataChanges_ShouldKeepSchemaVersionAndRecordMetadataHistory()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var original = CreateDefinition("Test.Concurrent.Metadata");
        var changed = original with { DisplayName = "Updated Metadata Display Name" };

        await stores.Items[0].PublishAsync(CreatePublication([original]), TestContext.Current.CancellationToken);
        await stores.Items[0].PublishAsync(CreatePublication([changed]), TestContext.Current.CancellationToken);

        var publishedEntry = await stores.Items[0].GetPublishedDefinitionEntryAsync(
            original.DefinitionKey,
            TestContext.Current.CancellationToken);
        var published = publishedEntry?.RequireDefinition();
        published.Should().NotBeNull();
        published!.SchemaVersion.Should().Be(1);
        published.DefinitionRevision.Should().Be(2);
        published.DisplayName.Should().Be(changed.DisplayName);

        var overview = await GetOverviewAsync(stores, original.DefinitionKey);
        overview.DefinitionRevision.Should().Be(2);
        overview.SchemaVersion.Should().Be(1);
        overview.RevisionHistories.Select(static history => history.DefinitionRevision)
            .Should().Equal(2, 1);
        overview.RevisionHistories.Select(static history => history.DefinitionRevision)
            .Should().OnlyHaveUniqueItems();
        overview.RevisionHistories.Select(static history => history.ChangeKind)
            .Should().Contain([ConfigurationDefinitionPublishChangeKind.Created, ConfigurationDefinitionPublishChangeKind.MetadataChanged]);
    }

    [Fact]
    public async Task PublishAsync_WhenSchemaChanges_ShouldIncrementSchemaVersionAndRecordSchemaHistory()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var stores = CreateStores(databasePath, 1);
        var original = CreateDefinition("Test.Concurrent.Schema");
        var changedRoot = original.Root with
        {
            Children =
            [
                original.Root.Children[0] with
                {
                    ClrTypeName = typeof(string).AssemblyQualifiedName!,
                    ValueKind = ConfigurationValueKind.String
                }
            ]
        };
        var changed = WithComputedSchemaHash(original with { Root = changedRoot });

        await stores.Items[0].PublishAsync(CreatePublication([original]), TestContext.Current.CancellationToken);
        await stores.Items[0].PublishAsync(CreatePublication([changed]), TestContext.Current.CancellationToken);

        var publishedEntry = await stores.Items[0].GetPublishedDefinitionEntryAsync(
            original.DefinitionKey,
            TestContext.Current.CancellationToken);
        var published = publishedEntry?.RequireDefinition();
        published.Should().NotBeNull();
        published!.SchemaVersion.Should().Be(2);
        published.DefinitionRevision.Should().Be(2);
        published.SchemaHash.Should().Be(changed.SchemaHash);

        var overview = await GetOverviewAsync(stores, original.DefinitionKey);
        overview.DefinitionRevision.Should().Be(2);
        overview.SchemaVersion.Should().Be(2);
        overview.RevisionHistories.Select(static history => history.DefinitionRevision)
            .Should().Equal(2, 1);
        overview.RevisionHistories.Select(static history => history.ChangeKind)
            .Should().Contain([ConfigurationDefinitionPublishChangeKind.Created, ConfigurationDefinitionPublishChangeKind.SchemaChanged]);
    }

    [Fact]
    public async Task ListPublishedDefinitionEntriesAsync_WhenDatabaseIsUnmigrated_ShouldFailWithoutCreatingSchema()
    {
        var databasePath = CreateDatabasePath();
        await using var stores = CreateStores(databasePath, 1);

        var act = () => stores.Items[0]
            .ListPublishedDefinitionEntriesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConfigurationStoreSchemaException>()
            .WithMessage("*EF Core migration*");
        var configurationTableCount = await CountConfigurationTablesAsync(databasePath);
        configurationTableCount.Should().Be(0);
    }

    private static async Task<string> CreateMigratedDatabaseAsync()
    {
        var databasePath = CreateDatabasePath();
        await using var provider = CreateProvider(databasePath);
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return databasePath;
    }

    private static async Task<IReadOnlyList<StoreLockRow>> ReadStoreLocksAsync(string databasePath)
    {
        await using var provider = CreateProvider(databasePath);
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT "LockKey", "LockVersion"
                              FROM "ConfigurationStoreLocks"
                              ORDER BY "LockKey"
                              """;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<StoreLockRow>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new StoreLockRow(reader.GetString(0), reader.GetInt64(1)));
        }

        return rows;
    }

    private static async Task DeleteStoreLockAsync(string databasePath, string lockKey)
    {
        await using var provider = CreateProvider(databasePath);
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"ConfigurationStoreLocks\" WHERE \"LockKey\" = {lockKey}",
            TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountConfigurationTablesAsync(
        string databasePath,
        string? exactTableName = null)
    {
        await using var provider = CreateProvider(databasePath);
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = exactTableName is null
            ? "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name LIKE 'Configuration%'"
            : "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = $tableName";
        if (exactTableName is not null)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = exactTableName;
            command.Parameters.Add(parameter);
        }

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
            providers.Select(provider => provider.GetRequiredService<IConfigurationMetadataStore>()).ToArray());
    }

    private static ServiceProvider CreateProvider(
        string databasePath,
        IInterceptor? interceptor = null)
    {
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseDbConfigurationStore(options =>
            {
                ConfigurationStoreTestContextFactory.ConfigureOptions(
                    options,
                    $"Data Source={databasePath}");
                if (interceptor is not null)
                {
                    options.AddInterceptors(interceptor);
                }
            }));
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddConfiguration(inputPlan);
        });
        builder.Services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IAuditPropertySetter, NoOpAuditPropertySetter>());
        return builder.Services.BuildServiceProvider();
    }

    private static ConfigurationDefinition CreateDefinition(string definitionKey)
    {
        var definition = new ConfigurationDefinition
        {
            DefinitionKey = definitionKey,
            SectionPath = definitionKey.Replace(".", ":", StringComparison.Ordinal),
            DisplayName = definitionKey,
            ClrTypeName = typeof(DatabaseConfigurationStorePublishTests).AssemblyQualifiedName!,
            FromProject = "Test.Project",
            Category = "Test",
            SchemaHash = string.Empty,
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
        return WithComputedSchemaHash(definition);
    }

    private static ConfigurationDefinition WithComputedSchemaHash(ConfigurationDefinition definition)
    {
        return definition with
        {
            SchemaHash = ConfigurationDefinitionSchemaCodec.ComputeSchemaHash(
                definition.DefinitionKey,
                definition.SectionPath,
                definition.Root)
        };
    }

    private static ConfigurationDefinitionPublicationBatch CreatePublication(
        IReadOnlyList<ConfigurationDefinition> definitions)
    {
        return ConfigurationDefinitionPublicationBatch.Create(
            new ConfigurationPublisherIdentity
            {
                PublisherKey = "Test.Service",
                InstanceId = "Test.Service:1",
                Name = "Test Service"
            },
            definitions);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "monica-configuration-efcore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "configuration.db");
    }

    private sealed class StoreSet(ServiceProvider[] providers, IConfigurationMetadataStore[] items) : IAsyncDisposable
    {
        public IConfigurationMetadataStore[] Items { get; } = items;

        public async ValueTask DisposeAsync()
        {
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }

    private sealed record StoreLockRow(string LockKey, long LockVersion);

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
