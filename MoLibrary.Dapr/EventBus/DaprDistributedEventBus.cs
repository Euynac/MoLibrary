using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.Dapr.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Utils;

namespace MoLibrary.Dapr.EventBus;

public class DaprDistributedEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<DistributedEventBusOptions> distributedEventBusOptions,
    IEventHandlerInvoker eventHandlerInvoker,
    IOptions<ModuleDaprEventBusOption> daprEventBusOptions,
    DaprClient client,
    IMoLocalEventBus localEventBus) : DistributedEventBusBase(serviceScopeFactory,
        distributedEventBusOptions,
        eventHandlerInvoker,
        localEventBus)
{
    private readonly DistributedEventBusOptions _distributedEventBusOptions = distributedEventBusOptions.Value;
    public DaprClient Client { get; } = client;
    protected ModuleDaprEventBusOption DaprEventBusOptions { get; } = daprEventBusOptions.Value;
    public override ITypeList<IMoEventHandler> GetDefaultHandlers()
    {
        return _distributedEventBusOptions.Handlers;
    }


    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        await Client.PublishEventAsync(pubsubName: DaprEventBusOptions.PubSubName, topicName: EventNameAttribute.GetNameOrDefault(eventType),
            eventData);
    }

    protected override async Task BulkPublishToEventBusAsync(Type eventType, IEnumerable<object> eventDataList)
    {
        foreach (var chunk in eventDataList.ToList().SplitIntoChunks(DaprEventBusOptions.BulkChunkSize))
        {
            await Client.BulkPublishEventAsync(pubsubName: DaprEventBusOptions.PubSubName, topicName: EventNameAttribute.GetNameOrDefault(eventType),
                chunk);
        }

    }
}
