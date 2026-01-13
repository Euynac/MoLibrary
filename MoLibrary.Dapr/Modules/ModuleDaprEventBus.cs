using System.Runtime.Versioning;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Dapr.EventBus;
using MoLibrary.EventBus.Abstractions;
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
        services.AddHostedService<DaprEventBusSubscriptionHostedService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register();
        DependsOnModule<ModuleHostedServiceGuide>().Register();
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
    [RequiresPreviewFeatures]
    public ModuleDaprEventBusGuide AddKeyedDaprEventBus(string key, Action<ModuleDaprEventBusOption> configureOptions)
    {
        ConfigureServices(context =>
        {
            // Configure options for this keyed instance
            context.Services.Configure(key, configureOptions);
            // Register keyed DaprEventBus with the specified serviceKey
            context.Services.AddKeyedSingleton<IMoDistributedEventBus>(key, (sp, _) =>
            {
                var options = Options.Create(sp.GetRequiredService<IOptionsSnapshot<ModuleDaprEventBusOption>>().Get(key));
                return ActivatorUtilities.CreateInstance<DistributedEventBusDaprEventBus>(sp, options, key);
            });

            // Register HostedService for this keyed EventBus
            context.Services.AddSingleton<IHostedService>(sp =>
            {
                var options = Options.Create(sp.GetRequiredService<IOptionsSnapshot<ModuleDaprEventBusOption>>().Get(key));
                return ActivatorUtilities.CreateInstance<DaprEventBusSubscriptionHostedService>(sp, options, key);
            });
        }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }
}

public class ModuleDaprEventBusOption : MoModuleOptionWithMinimalApi<ModuleDaprEventBus>
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
    /// Dead letter topic name for failed messages. Defaults to null.
    /// </summary>
    public string? DeadLetterTopic { get; set; }

    /// <summary>
    /// Enable debug logging for incoming message data.
    /// When enabled, logs the raw JSON payload of each received message.
    /// Useful for troubleshooting deserialization issues and inspecting message format.
    /// </summary>
    public bool EnableMessageDataDebugLogging { get; set; }

    // Health Check Integration Options

    /// <summary>
    /// Maximum time to wait for Dapr sidecar to become healthy during startup.
    /// Default: 60 seconds
    /// </summary>
    public TimeSpan SidecarHealthWaitTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether to fail fast if Dapr sidecar is unavailable during startup.
    /// When true, throws InvalidOperationException if sidecar is not healthy after waiting.
    /// When false (default), logs warning and continues in degraded mode without subscriptions.
    /// Note: This is separate from EnableFailFast in ModuleDaprClientOption.
    /// EnableFailFast triggers graceful shutdown, while this throws exception immediately.
    /// </summary>
    public bool FailFastOnSidecarUnavailable { get; set; } = false;
}