using System.Runtime.Versioning;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;
using Monica.EventBus.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprEventBusBuilderExtensions
{
    /// <summary>
    /// Registers Dapr as the distributed event bus provider.
    /// </summary>
    public static ModuleDaprEventBusGuide UseDaprProvider(this ModuleEventBusGuide guide,
        Action<ModuleDaprEventBusOption>? action = null)
    {
        guide.UseDistributedEventBus<DaprEventBusProvider>();
        return new ModuleDaprEventBusGuide().Register(action);
    }
}

[ModuleKey(EMoModuleKey.DaprEventBus)]
public class ModuleDaprEventBus(ModuleDaprEventBusOption option)
    : MoModule<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>(option),
      IEventBusProviderModule
{

    #region IEventBusProviderModule

    /// <inheritdoc />
    public ModuleKey ProvidesFor => EMoModuleKey.EventBus;

    /// <inheritdoc />
    public EventBusProviderKind ProviderType => EventBusProviderKind.Dapr;

    /// <inheritdoc />
    public EventBusProviderCapabilities Capabilities =>
        EventBusProviderCapabilities.BulkPublish |
        EventBusProviderCapabilities.Streaming |
        EventBusProviderCapabilities.DeadLetterQueue;

    /// <inheritdoc />
    public string DisplayName => "Dapr";

    #endregion

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
    /// Registers a keyed Dapr distributed event bus together with its corresponding hosted
    /// subscription service.
    /// </summary>
    /// <param name="key">Service key.</param>
    /// <param name="configureOptions">
    /// Optional Dapr event bus configuration, for example to use a different pub/sub component.
    /// </param>
    [RequiresPreviewFeatures]
    public ModuleDaprEventBusGuide AddKeyedDaprEventBus(string key, Action<ModuleDaprEventBusOption> configureOptions)
    {
        ConfigureServices(context =>
        {
            // Configure options for this keyed instance
            context.Services.Configure(key, configureOptions);
            // Register keyed DaprEventBus with the specified serviceKey
            context.Services.AddKeyedSingleton<IDistributedEventBus>(key, (sp, _) =>
            {
                var options = Options.Create(sp.GetRequiredService<IOptionsMonitor<ModuleDaprEventBusOption>>().Get(key));
                return ActivatorUtilities.CreateInstance<DaprEventBusProvider>(sp, options, key);
            });

            // Register HostedService for this keyed EventBus
            context.Services.AddSingleton<IHostedService>(sp =>
            {
                var options = Options.Create(sp.GetRequiredService<IOptionsMonitor<ModuleDaprEventBusOption>>().Get(key));
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
