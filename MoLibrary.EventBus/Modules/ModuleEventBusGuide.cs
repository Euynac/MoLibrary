using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Handlers;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Providers;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBusGuide : MoModuleGuide<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>
{
    /// <summary>
    /// 设置统一分布式事件总线Provider（默认，ServiceKey = null）
    /// </summary>
    public ModuleEventBusGuide SetDistributedEventBusProvider<TProvider>() where TProvider : DistributedEventBusBase
    {
        ConfigureServices(services =>
        {
            // Register concrete type first
            services.Services.AddSingleton<TProvider>();
            // Register interface pointing to concrete type
            services.Services.AddSingleton<IMoDistributedEventBus>(sp => sp.GetRequiredService<TProvider>());
        });
        return this;
    }

    /// <summary>
    /// 设置空的分布式事件总线（空实现），用于测试或不需要实际发布事件的场景
    /// </summary>
    public ModuleEventBusGuide SetDistributedEventBusNullProvider()
    {
        ConfigureServices(services =>
        {
            services.Services.AddSingleton<IMoDistributedEventBus, NullDistributedEventBus>();
        });
        return this;
    }

    /// <summary>
    /// 添加指定键的事件总线服务，根据useDistributed参数决定使用本地或分布式实现
    /// </summary>
    /// <param name="key">服务键</param>
    /// <param name="useDistributed">是否使用分布式EventBus，false则使用本地EventBus</param>
    public ModuleEventBusGuide AddKeyedCommonEventBus(string key, bool useDistributed = false)
    {
        ConfigureServices(context =>
        {
            if (useDistributed)
            {
                CheckRequiredMethod(nameof(SetDistributedEventBusProvider));
                // For distributed, delegate to the registered keyed IMoDistributedEventBus
                context.Services.TryAddKeyedSingleton<IMoEventBus>(key, (sp, _) =>
                    sp.GetRequiredService<IMoDistributedEventBus>());
            }
            else
            {
                // For local, delegate to the registered keyed IMoLocalEventBus
                context.Services.TryAddKeyedSingleton<IMoEventBus>(key, (sp, _) =>
                    sp.GetRequiredService<IMoLocalEventBus>());
            }
        }, secondKey: key);

        return this;
    }

    /// <summary>
    /// 添加Keyed本地事件总线（带ServiceKey的LocalEventBus实例）
    /// </summary>
    /// <param name="key">服务键</param>
    public ModuleEventBusGuide AddKeyedLocalEventBus(string key)
    {
        ConfigureServices(context =>
        {
            // Register keyed LocalEventBus with the specified serviceKey
            context.Services.AddKeyedSingleton<IMoLocalEventBus>(key, (sp, _) =>
                new LocalEventBus(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IEventHandlerInvoker>(),
                    sp.GetRequiredService<ISubscriptionManager>(),
                    sp.GetRequiredService<ILogger<LocalEventBus>>(),
                    serviceKey: key));
        }, secondKey: key);

        return this;
    }
}
