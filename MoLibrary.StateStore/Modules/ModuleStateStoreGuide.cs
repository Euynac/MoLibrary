using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.StateStore.Modules;

public class ModuleStateStoreGuide : MoModuleGuide<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>
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
                context.Services.TryAddKeyedSingleton<IMoStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IDistributedStateStore>());
            }
            else
            {
                context.Services.TryAddKeyedSingleton<IMoStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IMemoryStateStore>());
            }
        }, secondKey: key);

        return this;
    }

    /// <summary>
    /// Add a keyed abstract state store provider
    /// </summary>
    /// <typeparam name="TProvider">State store provider type</typeparam>
    /// <param name="key">Service key</param>
    /// <returns>Current module guide instance for chaining</returns>
    public ModuleStateStoreGuide AddKeyedStateStore<TProvider>(string key) where TProvider : class, IMoStateStore
    {
        ConfigureServices(services => { services.Services.AddKeyedSingleton<IMoStateStore, TProvider>(key); });
        return this;
    }

    /// <summary>
    /// Configure custom keyed StateStore service registration
    /// </summary>
    /// <param name="configureKeyedServices">Service configuration delegate</param>
    /// <returns>Current module guide instance for chaining</returns>
    public ModuleStateStoreGuide ConfigureKeyedStateStore(Action<IServiceCollection> configureKeyedServices)
    {
        ConfigureServices(context =>
        {
            configureKeyedServices(context.Services);
        });
        return this;
    }
}
