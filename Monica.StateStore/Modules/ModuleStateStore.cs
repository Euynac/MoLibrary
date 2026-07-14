using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
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
        public ModuleStateStoreGuide AddStateStore(Action<ModuleStateStoreOption>? action = null)
        {
            return builder.AddModule<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.StateStore)]
public class ModuleStateStore(ModuleStateStoreOption option)
    : ModuleBase<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IMemoryStateStore, MemoryCacheProvider>();
        if (Option.UseDistributedProviderAsDefault)
        {
            CheckRequiredMethod(nameof(ModuleStateStoreGuide.SetCommonDistributedStateStoreProvider), "未配置分布式状态存储Provider");
            services.AddSingleton<IStateStore>(serviceProvider =>
                serviceProvider.GetRequiredService<IDistributedStateStore>());
        }
        else
        {
            services.AddSingleton<IStateStore>(serviceProvider => serviceProvider.GetRequiredService<IMemoryStateStore>());
        }
    }
}

public class ModuleStateStoreGuide : ModuleGuide<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>
{
    /// <summary>
    /// Register a common distributed state store provider
    /// </summary>
    /// <typeparam name="TProvider">Distributed state store provider type</typeparam>
    /// <returns>Current module guide instance for chaining</returns>
    public ModuleStateStoreGuide SetCommonDistributedStateStoreProvider<TProvider>()
        where TProvider : class, IDistributedStateStore
    {
        ConfigureServices(context => { context.Services.AddSingleton<IDistributedStateStore, TProvider>(); });
        return this;
    }

    /// <summary>
    /// Add a keyed state store using the common provider
    /// </summary>
    /// <param name="key">Service key</param>
    /// <param name="useDistributed">Whether to use distributed storage, false uses memory storage</param>
    /// <returns>Current module guide instance for chaining</returns>
    public ModuleStateStoreGuide AddKeyedCommonStateStore(string key, bool useDistributed = false)
    {
        ConfigureServices(context =>
        {
            if (useDistributed)
            {
                CheckRequiredMethod(nameof(SetCommonDistributedStateStoreProvider));
                context.Services.TryAddKeyedSingleton<IStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IDistributedStateStore>());
            }
            else
            {
                context.Services.TryAddKeyedSingleton<IStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IMemoryStateStore>());
            }
        }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }

    /// <summary>
    /// Add a keyed abstract state store provider
    /// </summary>
    /// <typeparam name="TProvider">State store provider type</typeparam>
    /// <param name="key">Service key</param>
    /// <returns>Current module guide instance for chaining</returns>
    public ModuleStateStoreGuide AddKeyedStateStore<TProvider>(string key) where TProvider : class, IStateStore
    {
        ConfigureServices(services => { services.Services.AddKeyedSingleton<IStateStore, TProvider>(key); }, secondKey: key);
        RecordKeyedServiceKey(key);
        return this;
    }

    /// <summary>
    /// Configure custom StateStore service registration
    /// </summary>
    /// <param name="configureServices">Service configuration delegate</param>
    /// <param name="key">Optional key to differentiate multiple calls (used as secondKey in module system)</param>
    /// <returns>Current module guide instance for chaining</returns>
    public ModuleStateStoreGuide ConfigureStateStoreServices(Action<IServiceCollection> configureServices, string? key = null)
    {
        ConfigureServices(context =>
        {
            configureServices(context.Services);
        }, secondKey: key);
        return this;
    }
}

public class ModuleStateStoreOption : ModuleOptions<ModuleStateStore>
{
    /// <summary>
    /// Use distributed state storage as the default (non-Keyed service) <see cref="IStateStore"/> implementation
    /// </summary>
    public bool UseDistributedProviderAsDefault { get; set; }
}
