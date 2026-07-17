using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.EfCore.Stores.Support;
using Monica.Configuration.Models;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Repository;
using Monica.Repository.Entity.Abstractions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for EF Core persistence in Monica.Configuration.
/// </summary>
public static class ModuleConfigurationEfCoreBuilderExtensions
{
    /// <summary>
    /// Registers EF Core as the Monica.Configuration distributed store bundle.
    /// </summary>
    /// <param name="guide">The configuration module guide.</param>
    /// <param name="optionsAction">DbContext configuration.</param>
    /// <param name="configure">Optional EF Core store options.</param>
    /// <returns>The configuration module guide.</returns>
    public static ModuleConfigurationGuide UseDbConfigurationStore(
        this ModuleConfigurationGuide guide,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction,
        Action<ModuleConfigurationEfCoreOption>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(guide);
        ArgumentNullException.ThrowIfNull(optionsAction);

        new ModuleConfigurationEfCoreGuide().Register(configure).UseDbContext(optionsAction);
        return guide.UseStartupEffectiveValueStore(() => CreateStartupStore(optionsAction, configure));
    }

    private static IConfigurationEffectiveValueStore CreateStartupStore(
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction,
        Action<ModuleConfigurationEfCoreOption>? configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddOptions<ModuleRepositoryOption>();
        services.AddOptions<ModuleConfigurationEfCoreOption>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        services.AddSingleton<IAuditPropertySetter, NoOpAuditPropertySetter>();
        services.AddSingleton(
            typeof(IDbContextOperation<ConfigurationDbContext>),
            typeof(ScopedDbContextOperation<ConfigurationDbContext>));
        services.AddDbContext<ConfigurationDbContext>((serviceProvider, optionsBuilder) =>
        {
            optionsAction(serviceProvider, optionsBuilder);
        });
        AddDatabaseConfigurationStores(services);

        var serviceProvider = services.BuildServiceProvider();
        return new OwnedConfigurationEffectiveValueStore(
            serviceProvider,
            serviceProvider.GetRequiredService<DatabaseConfigurationEffectiveValueStore>());
    }

    internal static void AddDatabaseConfigurationStores(IServiceCollection services)
    {
        services.TryAddSingleton<ConfigurationDatabaseSchemaManager>(serviceProvider =>
            new ConfigurationDatabaseSchemaManager(
                serviceProvider.GetRequiredService<IDbContextOperation<ConfigurationDbContext>>(),
                serviceProvider.GetRequiredService<IOptions<ModuleConfigurationEfCoreOption>>()));
        services.TryAddSingleton<ConfigurationDatabase>(serviceProvider =>
            new ConfigurationDatabase(
                serviceProvider.GetRequiredService<IDbContextOperation<ConfigurationDbContext>>(),
                serviceProvider.GetRequiredService<ConfigurationDatabaseSchemaManager>()));
        services.TryAddSingleton<DatabaseConfigurationStore>(serviceProvider =>
            new DatabaseConfigurationStore(
                serviceProvider.GetRequiredService<ConfigurationDatabaseSchemaManager>()));
        services.TryAddSingleton<DatabaseConfigurationEffectiveValueStore>(serviceProvider =>
            new DatabaseConfigurationEffectiveValueStore(
                serviceProvider.GetRequiredService<ConfigurationDatabase>()));
        services.TryAddSingleton<DatabaseConfigurationHistoryStore>(serviceProvider =>
            new DatabaseConfigurationHistoryStore(
                serviceProvider.GetRequiredService<ConfigurationDatabase>()));
        services.TryAddSingleton<DatabaseConfigurationMetadataStore>(serviceProvider =>
            new DatabaseConfigurationMetadataStore(
                serviceProvider.GetRequiredService<ConfigurationDatabase>()));
        services.TryAddSingleton<DatabaseConfigurationUnifiedVersionStore>(serviceProvider =>
            new DatabaseConfigurationUnifiedVersionStore(
                serviceProvider.GetRequiredService<ConfigurationDatabase>()));
        services.TryAddSingleton<DatabaseConfigurationMutationBatchStore>(serviceProvider =>
            new DatabaseConfigurationMutationBatchStore(
                serviceProvider.GetRequiredService<ConfigurationDatabase>()));

        services.Replace(ServiceDescriptor.Singleton<IConfigurationEffectiveValueStore>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseConfigurationEffectiveValueStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationHistoryStore>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseConfigurationHistoryStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationMetadataStore>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseConfigurationMetadataStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationUnifiedVersionStore>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseConfigurationUnifiedVersionStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationMutationBatchStore>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseConfigurationMutationBatchStore>()));
    }

    private sealed class OwnedConfigurationEffectiveValueStore(
        ServiceProvider serviceProvider,
        IConfigurationEffectiveValueStore inner)
        : IConfigurationEffectiveValueStore, IDisposable, IAsyncDisposable
    {
        public ConfigurationStoreDescriptor Descriptor => inner.Descriptor;

        public Task<ConfigurationEffectiveValueDocument> EnsureCreatedAsync(
            ConfigurationDefinition definition,
            string seedJson,
            CancellationToken cancellationToken)
        {
            return inner.EnsureCreatedAsync(definition, seedJson, cancellationToken);
        }

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument>> EnsureCreatedAsync(
            IReadOnlyList<ConfigurationEffectiveValueSeed> seeds,
            CancellationToken cancellationToken)
        {
            return inner.EnsureCreatedAsync(seeds, cancellationToken);
        }

        public Task<ConfigurationEffectiveValueDocument?> GetAsync(
            string definitionKey,
            CancellationToken cancellationToken)
        {
            return inner.GetAsync(definitionKey, cancellationToken);
        }

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument?>> GetManyAsync(
            IReadOnlyList<string> definitionKeys,
            CancellationToken cancellationToken)
        {
            return inner.GetManyAsync(definitionKeys, cancellationToken);
        }

        public Task<ConfigurationEffectiveValueDocument> SaveAsync(
            ConfigurationEffectiveValueSaveRequest request,
            CancellationToken cancellationToken)
        {
            return inner.SaveAsync(request, cancellationToken);
        }

        public void Dispose()
        {
            serviceProvider.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return serviceProvider.DisposeAsync();
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

/// <summary>
/// EF Core module for Monica.Configuration.
/// </summary>
[ModuleKey("Monica.Configuration.EfCore")]
public sealed class ModuleConfigurationEfCore(ModuleConfigurationEfCoreOption option)
    : ModuleBase<ModuleConfigurationEfCore, ModuleConfigurationEfCoreOption, ModuleConfigurationEfCoreGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleConfigurationGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ModuleConfigurationEfCoreBuilderExtensions.AddDatabaseConfigurationStores(services);
    }
}

/// <summary>
/// Fluent guide for EF Core configuration persistence.
/// </summary>
public sealed class ModuleConfigurationEfCoreGuide
    : ModuleGuide<ModuleConfigurationEfCore, ModuleConfigurationEfCoreOption, ModuleConfigurationEfCoreGuide>
{
    /// <inheritdoc />
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(UseDbContext)];
    }

    /// <summary>
    /// Registers the configuration DbContext.
    /// </summary>
    /// <param name="optionsAction">DbContext configuration.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationEfCoreGuide UseDbContext(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        DependsOnModule<ModuleRepositoryGuide>().Register()
            .AddRepositoryDbContext<ConfigurationDbContext>(optionsAction);
        ConfigureEmpty();
        return this;
    }
}

/// <summary>
/// Module options for EF Core configuration persistence.
/// </summary>
public sealed class ModuleConfigurationEfCoreOption : ModuleOptions<ModuleConfigurationEfCore>
{
    /// <summary>
    /// Gets or sets whether the EF Core store automatically creates and validates the Monica.Configuration schema.
    /// </summary>
    /// <remarks>
    /// This is enabled by default because Monica.Configuration.EfCore is often added to an existing application database
    /// without a host-owned migration. The initializer creates all Monica.Configuration tables only when that schema is
    /// absent, and otherwise validates that the database already uses the current schema version. Disable this when the
    /// host controls schema creation explicitly, then resolve <see cref="DatabaseConfigurationStore"/> and call
    /// <see cref="DatabaseConfigurationStore.InitializeSchemaAsync(CancellationToken)"/> before configuration stores are
    /// used. Earlier schema versions are deliberately rejected; create a fresh version 8 database and retain the earlier
    /// database only as an external archive.
    /// </remarks>
    public bool AutoManageSchema { get; set; } = true;
}
