using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Providers.Memory;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleStateStoreBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the StateStore module
        /// </summary>
        public ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddStateStore(Action<ModuleStateStoreOption>? action = null)
        {
            return builder.AddModule<ModuleStateStore, ModuleStateStoreOption>(action);
        }
    }
}

public class ModuleStateStore : MonicaModule<ModuleStateStoreOption>
{
    /// <summary>
    /// Identifies the distributed-provider capability required by features that resolve
    /// <see cref="IDistributedStateStore"/>.
    /// </summary>
    public const string DISTRIBUTED_PROVIDER_FEATURE = "distributed-provider";

    public override void ConfigureServices(ModuleContext<ModuleStateStoreOption> context)
    {
        var services = context.Services;
        services.AddMemoryCache();
        services.AddSingleton<IMemoryStateStore, MemoryCacheProvider>();
        if (Option.UseDistributedProviderAsDefault)
        {
            services.AddSingleton<IStateStore>(serviceProvider =>
                serviceProvider.GetRequiredService<IDistributedStateStore>());
        }
        else
        {
            services.AddSingleton<IStateStore>(serviceProvider => serviceProvider.GetRequiredService<IMemoryStateStore>());
        }
    }
}

public static class ModuleStateStoreRegistrationExtensions
{
    /// <summary>
    /// Register a common distributed state store provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <typeparam name="TProvider">Distributed state store provider type</typeparam>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> SetCommonDistributedStateStoreProvider<TProvider>(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module)
        where TProvider : class, IDistributedStateStore
    {
        module.RequireFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
        return module
            .Configure(options => options.UseDistributedProviderAsDefault = true)
            .ConfigureServices(context => context.Services.AddSingleton<IDistributedStateStore, TProvider>())
            .SatisfyFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
    }

    /// <summary>
    /// Add a keyed state store using the common provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="key">Service key</param>
    /// <param name="useDistributed">Whether to use distributed storage, false uses memory storage</param>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddKeyedCommonStateStore(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module, string key, bool useDistributed = false)
    {
        if (useDistributed)
        {
            module.RequireFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
        }

        module.ConfigureServices(context =>
        {
            if (useDistributed)
            {
                context.Services.TryAddKeyedSingleton<IStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IDistributedStateStore>());
            }
            else
            {
                context.Services.TryAddKeyedSingleton<IStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IMemoryStateStore>());
            }
        });

        module.RecordKeyedServiceKey(key);
        return module;
    }

    /// <summary>
    /// Add a keyed abstract state store provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <typeparam name="TProvider">State store provider type</typeparam>
    /// <param name="key">Service key</param>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddKeyedStateStore<TProvider>(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module, string key) where TProvider : class, IStateStore
    {
        module.ConfigureServices(context => context.Services.AddKeyedSingleton<IStateStore, TProvider>(key));
        module.RecordKeyedServiceKey(key);
        return module;
    }

    /// <summary>
    /// Configure custom StateStore service registration
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="configureServices">Service configuration delegate</param>
    /// <param name="key">Optional key to differentiate multiple calls (used as secondKey in module system)</param>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> ConfigureStateStoreServices(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module, Action<IServiceCollection> configureServices, string? key = null)
    {
        module.ConfigureServices(context =>
        {
            configureServices(context.Services);
        });
        return module;
    }

}

public class ModuleStateStoreOption : ModuleOptions<ModuleStateStore>
{
    /// <summary>
    /// Use distributed state storage as the default (non-Keyed service) <see cref="IStateStore"/> implementation
    /// </summary>
    public bool UseDistributedProviderAsDefault { get; internal set; }
}
