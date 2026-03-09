using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Abstractions.Subscriptions;
using Monica.EventBus.Models;
using Monica.EventBus.Providers;
using Monica.EventBus.Subscriptions;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleEventBusBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 EventBus 模块
        /// </summary>
        public static ModuleEventBusGuide AddEventBus(Action<ModuleEventBusOption>? action = null)
        {
            return new ModuleEventBusGuide().Register(action);
        }
    }
}

public class ModuleEventBus(ModuleEventBusOption option)
    : MoModule<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>(option),
      IWantIterateBusinessTypes
{
    private readonly List<EventHandlerRegisterInfo> _autoDiscoveredHandlers = [];

    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.EventBus;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register core EventBus services (shared across all EventBus instances)
        services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
        services.AddSingleton<IMoLocalEventBus, LocalEventBus>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var sp = app.ApplicationServices;
        var subscriptionManager = sp.GetRequiredService<ISubscriptionManager>();
        var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        // Convert auto-discovered EventHandlerRegisterInfo to Subscriptions
        var descriptors = _autoDiscoveredHandlers.Where(h => h.IsAutoRegistered)
            .Select(handlerInfo => new SubscriptionDescriptor
            {
                ServiceKey = null, // Auto-discovered handlers register to default EventBus
                EventType = handlerInfo.EventType,
                TopicName = handlerInfo.TopicName,
                HandlerFactory = new IocEventHandlerFactory(serviceScopeFactory, handlerInfo.HandlerType),
                Scope = handlerInfo.IsDistributed ? SubscriptionScope.Distributed : SubscriptionScope.Local,
                IsAutoDiscovered = true
            })
            .ToList();

        // Batch subscribe synchronously during module initialization
        if (descriptors.Count != 0)
        {
            subscriptionManager.SubscribeBatchAsync(descriptors).GetAwaiter().GetResult();
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
       
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (Option.DisableAutoDiscovery)
            {
                yield return type;
                continue;
            }

            if (type is { IsClass: true, IsAbstract: false } && type.IsImplementInterface<IMoEventHandler>())
            {
                try
                {
                    // Use factory method - handles all reflection and validation
                    var registrations = EventHandlerRegisterInfo.CreateFromHandlerType(type);
                    foreach (var registration in registrations)
                    {
                        _autoDiscoveredHandlers.Add(registration);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Fail fast with clear error at startup
                    throw new InvalidOperationException(
                        $"Failed to register event handler '{type.GetCleanFullName()}': {ex.Message}", ex);
                }
            }

            yield return type;
        }
    }
}

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

        RecordKeyedServiceKey(key);
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
                ActivatorUtilities.CreateInstance<LocalEventBus>(sp, key));
        }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }
}

public class ModuleEventBusOption : MoModuleOptionWithMinimalApi<ModuleEventBus>
{
    /// <summary>
    /// 是否禁止自动注册实现了 <see cref="IMoDistributedEventHandler{TEvent}"/>以及 <see cref="IMoLocalEventHandler{TEvent}"/> 的类型
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }
}
