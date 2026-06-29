using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Bootstrap;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.Models;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services.Support;

namespace Monica.Configuration.EfCore.Bootstrap;

/// <summary>
/// EF Core store extensions for startup Monica effective-options readers.
/// </summary>
public static class MonicaEffectiveOptionsReaderEfCoreExtensions
{
    /// <summary>
    /// Uses the EF Core Monica.Configuration store for startup effective-options reads.
    /// </summary>
    /// <param name="builder">The reader builder.</param>
    /// <param name="optionsAction">Configures the database provider used by <see cref="ConfigurationDbContext"/>.</param>
    /// <param name="configure">Optional EF Core store options.</param>
    /// <returns>The same reader builder for chaining.</returns>
    public static MonicaEffectiveOptionsReaderBuilder UseDbConfigurationStore(
        this MonicaEffectiveOptionsReaderBuilder builder,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction,
        Action<ModuleConfigurationEfCoreOption>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(optionsAction);

        return builder.UseEffectiveValueStore(() => CreateStore(optionsAction, configure));
    }

    private static IConfigurationEffectiveValueStore CreateStore(
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
        services.AddSingleton(typeof(IDbContextOperation<ConfigurationDbContext>), typeof(ScopedDbContextOperation<ConfigurationDbContext>));
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
