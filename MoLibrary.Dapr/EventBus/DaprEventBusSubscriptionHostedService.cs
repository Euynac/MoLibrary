using System.Collections.Concurrent;
using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Dapr.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Services;

namespace MoLibrary.Dapr.EventBus;

/// <summary>
/// Dapr-specific implementation of subscription hosted service.
/// Manages Dapr streaming subscriptions for distributed events.
/// </summary>
internal class DaprEventBusSubscriptionHostedService(
    DaprPublishSubscribeClient daprClient,
    ISubscriptionManager subscriptionManager,
    IMoDistributedEventBus eventBus,
    IOptions<ModuleDaprEventBusOption> options,
    ILogger<DaprEventBusSubscriptionHostedService> logger,
    IGlobalJsonOption jsonOption,
    string? serviceKey = null)
    : EventBusSubscriptionHostedServiceBase(subscriptionManager, eventBus, logger, serviceKey)
{
    private readonly ModuleDaprEventBusOption _options = options.Value;

    // Track Dapr subscriptions by topic name
    private readonly ConcurrentDictionary<string, IAsyncDisposable> _daprSubscriptionsByTopic = new();

    /// <summary>
    /// Creates a Dapr streaming subscription for the given topic.
    /// </summary>
    protected override async Task CreateExternalSubscriptionForTopicAsync(
        string topicName,
        Type eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogDebug(
                "Creating Dapr subscription for topic {Topic} with EventType {EventType} (ServiceKey: {ServiceKey})",
                topicName, eventType.Name, ServiceKey ?? "default");

            // Message handler - deserializes and delegates to base class
            async Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken ct)
            {
                try
                {
                    // Deserialize message data using the topic's event type
                    var eventData = JsonSerializer.Deserialize(
                        message.Data.Span,
                        eventType,
                        jsonOption.GlobalOptions);

                    if (eventData == null)
                    {
                        Logger.LogWarning(
                            "Deserialized message for topic {Topic} was null",
                            message.Topic);
                        return TopicResponseAction.Drop;
                    }

                    // Delegate to base class for handler routing and invocation
                    await HandleExternalMessageAsync(
                        message.Topic,
                        eventData,
                        ct);

                    return TopicResponseAction.Success;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex,
                        "Error handling Dapr message for topic {Topic}",
                        message.Topic);
                    return TopicResponseAction.Drop;
                }
            }

            // Create Dapr streaming subscription
            var subscriptionOptions = new DaprSubscriptionOptions(
                new MessageHandlingPolicy(_options.MessageHandlingTimeout, TopicResponseAction.Retry))
            {
                DeadLetterTopic = _options.DeadLetterTopic,
                MaximumCleanupTimeout = _options.MaximumCleanupTimeout,
                MaximumQueuedMessages = _options.MaximumQueuedMessages
            };

            var daprSubscription = await daprClient.SubscribeAsync(
                _options.PubSubName,
                topicName,
                subscriptionOptions,
                HandleMessageAsync,
                cancellationToken);

            // Store Dapr subscription for cleanup later
            _daprSubscriptionsByTopic.TryAdd(topicName, daprSubscription);

            Logger.LogInformation(
                "Created Dapr subscription for topic {Topic} with EventType {EventType} (ServiceKey: {ServiceKey})",
                topicName, eventType.Name, ServiceKey ?? "default");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Failed to create Dapr subscription for topic {Topic}",
                topicName);
            throw;
        }
    }

    /// <summary>
    /// Removes a Dapr subscription for the given topic.
    /// </summary>
    protected override async Task RemoveExternalSubscriptionForTopicAsync(
        string topicName,
        CancellationToken cancellationToken)
    {
        if (_daprSubscriptionsByTopic.TryRemove(topicName, out var daprSubscription))
        {
            try
            {
                Logger.LogInformation(
                    "Disposing Dapr subscription for topic {Topic} (ServiceKey: {ServiceKey})",
                    topicName, ServiceKey ?? "default");

                await daprSubscription.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error disposing Dapr subscription for topic {Topic}",
                    topicName);
                throw;
            }
        }
        else
        {
            Logger.LogWarning(
                "Attempted to remove Dapr subscription for topic {Topic}, but it was not found",
                topicName);
        }
    }
}
