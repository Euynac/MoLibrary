using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Dapr.Services;

/// <summary>
/// Dapr-based distributed event bus implementation.
/// Publishes events using Dapr PubSub component.
/// </summary>
public class DaprEventBusProvider(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    DaprClient daprClient,
    IOptions<ModuleDaprEventBusOption> daprOptions,
    string? serviceKey = null)
    : DistributedEventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, serviceKey)
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly ModuleDaprEventBusOption _daprOptions = daprOptions.Value ?? throw new ArgumentNullException(nameof(daprOptions));

    /// <summary>
    /// Publishes an event to Dapr PubSub.
    /// </summary>
    public override async Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);

        Logger.LogDebug(
            "Publishing event {EventType} to Dapr topic {Topic} on PubSub {PubSubName}",
            eventType.Name, finalTopicName, _daprOptions.PubSubName);

        await _daprClient.PublishEventAsync(
            _daprOptions.PubSubName,
            finalTopicName,
            eventData,
            cancellationToken);
    }

    /// <summary>
    /// Publishes multiple events in bulk to Dapr PubSub.
    /// </summary>
    public override async Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        var eventsList = eventDataList.ToList();

        Logger.LogDebug(
            "Bulk publishing {Count} events of type {EventType} to Dapr topic {Topic} on PubSub {PubSubName}",
            eventsList.Count, eventType.Name, finalTopicName, _daprOptions.PubSubName);

        foreach (var chunk in eventsList.SplitIntoChunks(_daprOptions.BulkChunkSize))
        {
            await _daprClient.BulkPublishEventAsync(_daprOptions.PubSubName, finalTopicName, chunk, metadata: null, cancellationToken);
        }
    }
}
