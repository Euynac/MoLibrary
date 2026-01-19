using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.MemoryProvider;

namespace MoLibrary.StateStore.Modules;

public static class ModuleStateStoreBuilderExtensions
{
    public static ModuleStateStoreGuide ConfigModuleStateStore(this WebApplicationBuilder builder,
        Action<ModuleStateStoreOption>? action = null)
    {
        return new ModuleStateStoreGuide().Register(action);
    }
}

public class ModuleStateStore(ModuleStateStoreOption option)
    : MoModule<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.StateStore;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IMemoryStateStore, MemoryCacheProvider>();
        if (Option.UseDistributedProviderAsDefault)
        {
            CheckRequiredMethod(nameof(ModuleStateStoreGuide.SetCommonDistributedStateStoreProvider), "未配置分布式状态存储Provider");
            services.AddSingleton<IMoStateStore>(serviceProvider =>
                serviceProvider.GetRequiredService<IDistributedStateStore>());
        }
        else
        {
            services.AddSingleton<IMoStateStore>(serviceProvider => serviceProvider.GetRequiredService<IMemoryStateStore>());
        }
    }
}

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

        RecordKeyedServiceKey(key);
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

public class ModuleStateStoreOption : MoModuleOption<ModuleStateStore>
{
    /// <summary>
    /// 使用分布式状态存储作为默认的（非Keyed服务） <see cref="IMoStateStore"/> 实现
    /// </summary>
    public bool UseDistributedProviderAsDefault { get; set; }
}
