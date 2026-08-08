using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.Models;
using Monica.Core.Modularity.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services.Support;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Provides EF Core Configuration input-plan declarations.
/// </summary>
public static class MonicaConfigurationInputPlanEfCoreBuilderExtensions
{
    /// <summary>
    /// Selects the EF Core store bundle for read-only startup effective-options loading and runtime configuration.
    /// </summary>
    /// <param name="builder">The input-plan declaration builder.</param>
    /// <param name="configureDbContext">Configures the shared database provider and connection.</param>
    /// <returns>The same declaration builder.</returns>
    /// <remarks>
    /// The callback may be invoked independently for startup and runtime contexts. It must be deterministic and
    /// side-effect-free; captured values must be initialized before the first store operation and remain stable across
    /// both phases. Startup loading runs through a read-only reader in an isolated service provider. The host must apply
    /// <see cref="ConfigurationDbContext"/> migrations before startup loading; this declaration never creates or upgrades
    /// database tables.
    /// </remarks>
    public static MonicaConfigurationInputPlanBuilder UseDbConfigurationStore(
        this MonicaConfigurationInputPlanBuilder builder,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        return builder.UseConfigurationStore(
            new DatabaseConfigurationStoreComposition(configureDbContext));
    }

    private sealed class DatabaseConfigurationStoreComposition(
        Action<DbContextOptionsBuilder> configureDbContext)
        : IMonicaConfigurationStoreComposition
    {
        public string Name => "EF Core";

        public void ConfigureRuntime(
            ModuleRegistration<ModuleConfiguration, ModuleConfigurationOption> module)
        {
            module.Include<ModuleConfigurationEfCore, ModuleConfigurationEfCoreOption>()
                .UseDbContext((_, options) => configureDbContext(options));
        }

        public IConfigurationEffectiveValueReader CreateStartupReader()
        {
            return DatabaseEffectiveOptionsSnapshotReaderFactory.Create(configureDbContext);
        }
    }
}

internal static class DatabaseEffectiveOptionsSnapshotReaderFactory
{
    public static IConfigurationEffectiveValueReader Create(
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<ModuleRepositoryOption>();
        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        services.AddSingleton<IAuditPropertySetter, NoOpAuditPropertySetter>();
        services.AddSingleton(
            typeof(IDbContextOperation<ConfigurationDbContext>),
            typeof(ScopedDbContextOperation<ConfigurationDbContext>));
        services.AddDbContext<ConfigurationDbContext>(configureDbContext);
        ModuleConfigurationEfCore.AddDatabaseConfigurationStores(services);

        var serviceProvider = services.BuildServiceProvider();
        try
        {
            return new OwnedConfigurationEffectiveValueReader(
                serviceProvider,
                serviceProvider.GetRequiredService<DatabaseConfigurationEffectiveValueStore>());
        }
        catch
        {
            serviceProvider.Dispose();
            throw;
        }
    }

    private sealed class OwnedConfigurationEffectiveValueReader(
        ServiceProvider serviceProvider,
        IConfigurationEffectiveValueReader inner)
        : IConfigurationEffectiveValueReader, IDisposable, IAsyncDisposable
    {
        public ConfigurationStoreDescriptor Descriptor => inner.Descriptor;

        public Task<ConfigurationEffectiveValueDocument?> GetAsync(
            string definitionKey,
            CancellationToken cancellationToken) =>
            inner.GetAsync(definitionKey, cancellationToken);

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument?>> GetManyAsync(
            IReadOnlyList<string> definitionKeys,
            CancellationToken cancellationToken) =>
            inner.GetManyAsync(definitionKeys, cancellationToken);

        public void Dispose() => serviceProvider.Dispose();

        public ValueTask DisposeAsync() => serviceProvider.DisposeAsync();
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
