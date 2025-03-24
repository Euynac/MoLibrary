using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.Attributes;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.Tool.Utils;

namespace MoLibrary.EventBus.Dapr;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IDistributedEventBus), typeof(DaprDistributedEventBus))]
public class DaprDistributedEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<DistributedEventBusOptions> distributedEventBusOptions,
    IEventHandlerInvoker eventHandlerInvoker,
    IOptions<DaprEventBusOptions> daprEventBusOptions,
    DaprClient client,
    ILocalEventBus localEventBus) : DistributedEventBusBase(serviceScopeFactory,
        distributedEventBusOptions,
        eventHandlerInvoker,
        localEventBus), ISingletonDependency
{
    private readonly DistributedEventBusOptions _distributedEventBusOptions = distributedEventBusOptions.Value;
    public DaprClient Client { get; } = client;
    protected DaprEventBusOptions DaprEventBusOptions { get; } = daprEventBusOptions.Value;
    public override ITypeList<IEventHandler> GetDefaultHandlers()
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
        //TODO 需要研究批量的响应方法
        foreach (var o in eventDataList)
        {
            await PublishToEventBusAsync(eventType, o);
        }

        //await Client.BulkPublishEventAsync(pubsubName: DaprEventBusOptions.PubSubName, topicName: EventNameAttribute.GetNameOrDefault(eventType),
        //    eventDataList.ToList());
    }
}
