using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.Models;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services.Support;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Configures EF Core persistence for startup effective-options readers.
/// </summary>
public static class MonicaEffectiveOptionsReaderConfigurationExtensions
{
    /// <summary>
    /// Uses the Monica.Configuration EF Core store for a caller-owned startup reader.
    /// </summary>
    /// <param name="configuration">The startup reader configuration.</param>
    /// <param name="configureDbContext">Configures the database provider and connection used by the reader.</param>
    /// <returns>The same reader configuration.</returns>
    /// <remarks>
    /// The reader owns an isolated service provider and disposes it with the reader. Use the same DbContext callback
    /// for this configuration and <see cref="ModuleConfigurationEfCoreBuilderExtensions.UseDbConfigurationStore"/>
    /// so startup reads and the runtime Configuration module target the same store.
    /// </remarks>
    public static MonicaEffectiveOptionsReaderConfiguration UseDbConfigurationStore(
        this MonicaEffectiveOptionsReaderConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        return configuration.UseEffectiveValueStore(
            () => DatabaseEffectiveOptionsReaderStoreFactory.Create(configureDbContext));
    }
}

internal static class DatabaseEffectiveOptionsReaderStoreFactory
{
    public static IConfigurationEffectiveValueStore Create(
        Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddOptions<ModuleRepositoryOption>();
        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        services.AddSingleton<IAuditPropertySetter, NoOpAuditPropertySetter>();
        services.AddSingleton(
            typeof(IDbContextOperation<ConfigurationDbContext>),
            typeof(ScopedDbContextOperation<ConfigurationDbContext>));
        services.AddDbContext<ConfigurationDbContext>(configureDbContext);
        ModuleConfigurationEfCoreBuilderExtensions.AddDatabaseConfigurationStores(services);

        var serviceProvider = services.BuildServiceProvider();
        return new OwnedConfigurationEffectiveValueStore(
            serviceProvider,
            serviceProvider.GetRequiredService<DatabaseConfigurationEffectiveValueStore>());
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
            CancellationToken cancellationToken) =>
            inner.EnsureCreatedAsync(definition, seedJson, cancellationToken);

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument>> EnsureCreatedAsync(
            IReadOnlyList<ConfigurationEffectiveValueSeed> seeds,
            CancellationToken cancellationToken) =>
            inner.EnsureCreatedAsync(seeds, cancellationToken);

        public Task<ConfigurationEffectiveValueDocument?> GetAsync(
            string definitionKey,
            CancellationToken cancellationToken) =>
            inner.GetAsync(definitionKey, cancellationToken);

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument?>> GetManyAsync(
            IReadOnlyList<string> definitionKeys,
            CancellationToken cancellationToken) =>
            inner.GetManyAsync(definitionKeys, cancellationToken);

        public Task<ConfigurationEffectiveValueDocument> SaveAsync(
            ConfigurationEffectiveValueSaveRequest request,
            CancellationToken cancellationToken) =>
            inner.SaveAsync(request, cancellationToken);

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
