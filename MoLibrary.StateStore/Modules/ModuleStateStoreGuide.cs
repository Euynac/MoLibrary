using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.StateStore.Modules;

public class ModuleStateStoreGuide : MoModuleGuide<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>
{
    /// <summary>
    /// 注册统一分布式状态存储服务提供者
    /// </summary>
    /// <typeparam name="TProvider">分布式状态存储服务提供者类型</typeparam>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
    public ModuleStateStoreGuide SetCommonDistributedStateStoreProvider<TProvider>()
        where TProvider : class, IDistributedStateStore
    {
        ConfigureServices(context => { context.Services.AddSingleton<IDistributedStateStore, TProvider>(); });
        return this;
    }

    /// <summary>
    /// 添加指定键的状态存储服务，使用统一的提供者
    /// </summary>
    /// <param name="key">服务键</param>
    /// <param name="useDistributed">是否使用分布式存储，false则使用内存存储</param>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
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
    /// 添加Keyed抽象状态存储Provider
    /// </summary>
    /// <typeparam name="TProvider"></typeparam>
    /// <param name="key">服务键</param>
    /// <returns></returns>
    public ModuleStateStoreGuide AddKeyedStateStore<TProvider>(string key) where TProvider : class, IMoStateStore
    {
        ConfigureServices(services => { services.Services.AddKeyedSingleton<IMoStateStore, TProvider>(key); });
        return this;
    }
}