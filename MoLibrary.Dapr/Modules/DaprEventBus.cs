using Dapr.Client;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.EventBus;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Modules;

namespace MoLibrary.Dapr.Modules;

public static class ModuleDaprEventBusBuilderExtensions
{
    /// <summary>
    /// 使用Dapr作为分布式事件总线Provider
    /// </summary>
    public static ModuleDaprEventBusGuide UseDaprProvider(this ModuleEventBusGuide guide,
        Action<ModuleDaprEventBusOption>? action = null)
    {
        guide.SetDistributedEventBusProvider<DistributedEventBusDaprEventBus>();
        return new ModuleDaprEventBusGuide().Register(action);
    }
}

public class ModuleDaprEventBus(ModuleDaprEventBusOption option)
    : MoModuleWithDependencies<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprEventBus;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register DaprPublishSubscribeClient for streaming subscriptions
        services.AddDaprPubSubClient();

        // Register hosted service to manage dynamic Dapr subscriptions (default, ServiceKey = null)
        services.AddHostedService<DaprEventBusSubscriptionHostedService>(sp =>
            new DaprEventBusSubscriptionHostedService(
                sp.GetRequiredService<global::Dapr.Messaging.PublishSubscribe.DaprPublishSubscribeClient>(),
                sp.GetRequiredService<ISubscriptionManager>(),
                sp.GetRequiredService<IMoDistributedEventBus>(),
                sp.GetRequiredService<IOptions<ModuleDaprEventBusOption>>(),
                sp.GetRequiredService<ILogger<DaprEventBusSubscriptionHostedService>>(),
                serviceKey: null));
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register();
    }
}

public class ModuleDaprEventBusGuide : MoModuleGuide<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>
{
    /// <summary>
    /// 添加Keyed分布式Dapr事件总线
    /// 注册带有指定ServiceKey的DaprEventBus实例和对应的HostedService
    /// </summary>
    /// <param name="key">服务键</param>
    /// <param name="configureOptions">可选的Dapr配置（如不同的PubSubName）</param>
    public ModuleDaprEventBusGuide AddKeyedDaprEventBus(string key, Action<ModuleDaprEventBusOption>? configureOptions = null)
    {
        ConfigureServices(context =>
        {
            // Configure options for this keyed instance if provided
            if (configureOptions != null)
            {
                context.Services.Configure(key, configureOptions);
            }

            // Register keyed DaprEventBus with the specified serviceKey
            context.Services.AddKeyedSingleton<IMoDistributedEventBus>(key, (sp, _) =>
            {
                IOptions<ModuleDaprEventBusOption> options;
                if (configureOptions != null)
                {
                    options = Options.Create(sp.GetRequiredService<IOptionsSnapshot<ModuleDaprEventBusOption>>().Get(key));
                }
                else
                {
                    options = sp.GetRequiredService<IOptions<ModuleDaprEventBusOption>>();
                }

                return new DistributedEventBusDaprEventBus(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IEventHandlerInvoker>(),
                    sp.GetRequiredService<ISubscriptionManager>(),
                    sp.GetRequiredService<DaprClient>(),
                    options,
                    sp.GetRequiredService<ILogger<DistributedEventBusDaprEventBus>>(),
                    serviceKey: key);
            });

            // Register HostedService for this keyed EventBus
            context.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp =>
            {
                IOptions<ModuleDaprEventBusOption> hostedOptions;
                if (configureOptions != null)
                {
                    hostedOptions = Options.Create(sp.GetRequiredService<IOptionsSnapshot<ModuleDaprEventBusOption>>().Get(key));
                }
                else
                {
                    hostedOptions = sp.GetRequiredService<IOptions<ModuleDaprEventBusOption>>();
                }

                return new DaprEventBusSubscriptionHostedService(
                    sp.GetRequiredService<global::Dapr.Messaging.PublishSubscribe.DaprPublishSubscribeClient>(),
                    sp.GetRequiredService<ISubscriptionManager>(),
                    sp.GetRequiredKeyedService<IMoDistributedEventBus>(key),
                    hostedOptions,
                    sp.GetRequiredService<ILogger<DaprEventBusSubscriptionHostedService>>(),
                    serviceKey: key);
            });
        }, secondKey: key);

        return this;
    }
}

public class ModuleDaprEventBusOption : MoModuleControllerOption<ModuleDaprEventBus>
{
    public string PubSubName { get; set; } = "pubsub";

    /// <summary>
    /// Bulk chunk size for BulkPublishEventAsync. Defaults to 1000.
    /// </summary>
    public int? BulkChunkSize { get; set; } = 1000;

    /// <summary>
    /// Message handling timeout for streaming subscriptions. Defaults to 10 seconds.
    /// If a handler takes longer than this, Dapr will retry the message.
    /// </summary>
    public TimeSpan MessageHandlingTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of messages to queue for processing. Defaults to 100.
    /// Provides backpressure when handlers are slower than message arrival rate.
    /// </summary>
    public int MaximumQueuedMessages { get; set; } = 100;

    /// <summary>
    /// Maximum time to wait for queued messages to process during shutdown. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan MaximumCleanupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Dead letter topic name for failed messages. Defaults to "molibrary-eventbus-dlq".
    /// </summary>
    public string DeadLetterTopic { get; set; } = "molibrary-eventbus-dlq";
}
