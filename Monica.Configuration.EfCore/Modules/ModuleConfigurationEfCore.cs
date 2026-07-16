using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
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
        services.AddSingleton<DatabaseConfigurationStore>();

        var serviceProvider = services.BuildServiceProvider();
        return new OwnedConfigurationEffectiveValueStore(
            serviceProvider,
            serviceProvider.GetRequiredService<DatabaseConfigurationStore>());
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
        services.TryAddSingleton<DatabaseConfigurationStore>();
        services.Replace(ServiceDescriptor.Singleton<IConfigurationEffectiveValueStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationHistoryStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationMetadataStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationUnifiedVersionStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationMutationBatchStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
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
    /// Gets or sets whether the EF Core store automatically creates and upgrades the Monica.Configuration schema.
    /// </summary>
    /// <remarks>
    /// This is enabled by default because Monica.Configuration.EfCore is often added to an existing application database
    /// without a host-owned migration. The initializer creates missing Monica.Configuration tables and applies versioned,
    /// additive upgrades and required data backfills. Disable this when the host controls schema changes explicitly, then
    /// resolve <see cref="DatabaseConfigurationStore"/> and call
    /// <see cref="DatabaseConfigurationStore.UpgradeSchemaAsync(CancellationToken)"/> from the host's deployment or
    /// startup migration step before the configuration stores are used. On a populated earlier schema, a stock generated
    /// EF Core migration cannot compute provider-independent identities before creating unique indexes; omit or defer
    /// those generated identity operations and let <c>UpgradeSchemaAsync</c> perform them, or customize the migration to
    /// perform the equivalent backfill before it creates the indexes and advances the Configuration schema marker.
    /// </remarks>
    public bool AutoManageSchema { get; set; } = true;
}
