using System.Runtime.Versioning;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
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
        return guide.AddModule<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>(action);
    }
}

[ModuleKey(BuiltInModuleKey.DaprEventBus)]
public class ModuleDaprEventBus(ModuleDaprEventBusOption option)
    : ModuleBase<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>(option),
      IEventBusProviderModule
{

    #region IEventBusProviderModule

    /// <inheritdoc />
    public ModuleKey ProvidesFor => BuiltInModuleKey.EventBus;

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

public class ModuleDaprEventBusGuide : ModuleGuide<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>
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

public class ModuleDaprEventBusOption : MinimalApiModuleOptions<ModuleDaprEventBus>
{
    public string PubSubName { get; set; } = "pubsub";

    /// <summary>
    /// Bulk chunk size for BulkPublishEventAsync. Defaults to 1000.
    /// </summary>
    public int? BulkChunkSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the message-handling deadline passed to the Dapr streaming subscription. Defaults to 30 seconds.
    /// When the deadline elapses, Monica stops awaiting the handler and requests a retry. The same cancellation token
    /// is passed to the handler for cooperative shutdown, but non-cooperative handler code may continue running.
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
    /// Gets or sets the suffix appended to each source topic to derive its dedicated dead-letter topic.
    /// Defaults to <see langword="null" />, which disables dead-letter forwarding. For example, the suffix
    /// <c>.dead-letter</c> maps <c>orders</c> to <c>orders.dead-letter</c>. A subscription whose source topic already
    /// ends with this suffix is treated as terminal and receives no further dead-letter target, preventing recursive
    /// forwarding. Configuring this option does not provision broker topics or define Dapr retry limits; the host must
    /// deploy those resources separately before enabling forwarding. A configured suffix must be non-empty and contain
    /// no whitespace; invalid values are rejected when the subscription hosted service is activated.
    /// </summary>
    public string? DeadLetterTopicSuffix { get; set; }

    /// <summary>
    /// Gets or sets the initial delay before reconnecting a failed streaming subscription. Defaults to one second.
    /// Connection recovery is independent from message redelivery and continues until the host stops.
    /// The value must be greater than zero and no longer than <see cref="SubscriptionRecoveryMaxDelay" />.
    /// Monica applies up to 20 percent deterministic per-topic jitter below the calculated delay to avoid reconnect herds.
    /// </summary>
    public TimeSpan SubscriptionRecoveryInitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between streaming-subscription recovery attempts. Defaults to 30 seconds.
    /// The cap prevents prolonged outages from producing either a hot loop or unbounded backoff.
    /// The value must be at least <see cref="SubscriptionRecoveryInitialDelay" />.
    /// </summary>
    public TimeSpan SubscriptionRecoveryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the exponential multiplier applied after consecutive subscription connection failures.
    /// Defaults to <c>2</c>. The value must be finite and at least <c>1</c>.
    /// </summary>
    public double SubscriptionRecoveryBackoffMultiplier { get; set; } = 2d;

    /// <summary>
    /// Gets or sets how long a streaming receiver must remain fault-free before its recovery backoff is reset.
    /// Defaults to 30 seconds. This prevents a receiver that repeatedly fails immediately after creation from
    /// reconnecting forever at the initial delay.
    /// </summary>
    public TimeSpan SubscriptionRecoveryStabilityPeriod { get; set; } = TimeSpan.FromSeconds(30);

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
