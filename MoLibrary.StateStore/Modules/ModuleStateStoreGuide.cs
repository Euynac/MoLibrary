using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.StateStore.Modules;

public class ModuleStateStoreGuide : MoModuleGuide<ModuleStateStore, ModuleStateStoreOption, ModuleStateStoreGuide>
{
    /// <summary>
    /// 注册分布式状态存储服务提供者
    /// </summary>
    /// <typeparam name="TProvider">分布式状态存储服务提供者类型</typeparam>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
    public ModuleStateStoreGuide RegisterDistributedStateStoreProvider<TProvider>() where TProvider : class, IDistributedStateStore
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IDistributedStateStore, TProvider>();
        });
        return this;
    }

    /// <summary>
    /// 注册指定键的状态存储服务
    /// </summary>
    /// <param name="key">服务键</param>
    /// <param name="useDistributed">是否使用分布式存储，false则使用内存存储</param>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
    public ModuleStateStoreGuide AddKeyedStateStore(string key, bool useDistributed = false)
    {
        ConfigureServices(context =>
            {
                if (useDistributed)
                {
                    CheckRequiredMethod(nameof(RegisterDistributedStateStoreProvider));
                    context.Services.AddKeyedSingleton<IMoStateStore>(key, (serviceProvider, _) =>
                        serviceProvider.GetRequiredService<IDistributedStateStore>());
                }
                else
                {
                    context.Services.AddKeyedSingleton<IMoStateStore>(key, (serviceProvider, _) =>
                        serviceProvider.GetRequiredService<IMemoryStateStore>());
                }
            }, key: $"{nameof(AddKeyedStateStore)}_{key}");

        return this;
    }

}