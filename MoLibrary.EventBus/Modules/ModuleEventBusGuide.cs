using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Abstractions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBusGuide : MoModuleGuide<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>
{
    /// <summary>
    /// 设置统一分布式事件总线Provider
    /// </summary>
    /// <typeparam name="TProvider"></typeparam>
    /// <returns></returns>
    public ModuleEventBusGuide SetDistributedEventBusProvider<TProvider>() where TProvider : class, IMoDistributedEventBus
    {
        ConfigureServices(services =>
        {
            services.Services.AddSingleton<IMoDistributedEventBus, TProvider>();
            services.Services.AddSingleton<TProvider>();
        });
        return this;
    }

    /// <summary>
    /// 设置空的分布式事件总线（空实现），用于测试或不需要实际发布事件的场景
    /// </summary>
    /// <returns></returns>
    public ModuleEventBusGuide SetDistributedEventBusNullProvider()
    {
        ConfigureServices(services =>
        {
            services.Services.AddSingleton<IMoDistributedEventBus, NullDistributedEventBus>();
        });
        return this;
    }
    /// <summary>
    /// 添加指定键的状态存储服务，使用统一的提供者
    /// </summary>
    /// <param name="key">服务键</param>
    /// <param name="useDistributed">是否使用分布式存储，false则使用内存存储</param>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
    public ModuleEventBusGuide AddKeyedCommonEventBus(string key, bool useDistributed = false)
    {
        ConfigureServices(context =>
        {
            if (useDistributed)
            {
                CheckRequiredMethod(nameof(SetDistributedEventBusProvider));
                context.Services.TryAddKeyedSingleton<IMoEventBus>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IMoDistributedEventBus>());
            }
            else
            {
                context.Services.TryAddKeyedSingleton<IMoEventBus>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IMoLocalEventBus>());
            }
        }, secondKey: key);

        return this;
    }
    /// <summary>
    /// 添加Keyed抽象事件总线Provider
    /// </summary>
    /// <typeparam name="TProvider"></typeparam>
    /// <param name="key">服务键</param>
    /// <returns></returns>
    public ModuleEventBusGuide AddKeyedEventBus<TProvider>(string key) where TProvider : class, IMoEventBus
    {
        ConfigureServices(services =>
        {
            services.Services.AddKeyedSingleton<IMoEventBus, TProvider>(key);
            services.Services.AddKeyedSingleton<TProvider>(key);
        });
        return this;
    }
}