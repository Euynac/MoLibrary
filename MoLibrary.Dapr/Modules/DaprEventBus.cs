using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.EventBus;
using MoLibrary.EventBus.Modules;

namespace MoLibrary.Dapr.Modules;
public static class ModuleDaprEventBusBuilderExtensions
{
    public static ModuleDaprEventBusGuide UseDaprProvider(this ModuleEventBusGuide guide,
        Action<ModuleDaprEventBusOption>? action = null)
    {
        guide.SetDistributedEventBusProvider<DistributedEventBusDaprProvider>();
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
        // Register DaprPublishSubscribeClient
        services.AddDaprPubSubClient();

        // Register streaming subscription hosted service
        services.AddHostedService<DaprEventBusSubscriptionHostedService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register();
    }
}

public class
    ModuleDaprEventBusGuide : MoModuleGuide<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>
{


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
    public TimeSpan MessageHandlingTimeout { get; set; } = TimeSpan.FromSeconds(10);

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