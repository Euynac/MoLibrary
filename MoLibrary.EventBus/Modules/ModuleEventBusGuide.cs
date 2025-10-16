using Microsoft.Extensions.DependencyInjection;
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
    /// 设置Keyed抽象事件总线Provider
    /// </summary>
    /// <typeparam name="TProvider"></typeparam>
    /// <returns></returns>
    public ModuleEventBusGuide SetKeyedEventBus<TProvider>(string key) where TProvider : class, IMoEventBus
    {
        ConfigureServices(services =>
        {
            services.Services.AddKeyedSingleton<IMoEventBus, TProvider>(key);
            services.Services.AddKeyedSingleton<TProvider>(key);
        });
        return this;
    }
}