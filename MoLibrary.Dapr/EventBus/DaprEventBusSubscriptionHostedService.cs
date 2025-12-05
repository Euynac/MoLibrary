using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IMoEventBus eventBus,
    IOptions<ModuleDaprEventBusOption> options,
    ILogger<DaprEventBusSubscriptionHostedService> logger,
    string? serviceKey = null)
    : EventBusSubscriptionHostedServiceBase(subscriptionManager, eventBus, logger, serviceKey)
{
    private readonly ModuleDaprEventBusOption _options = options.Value;

    /// <summary>
    /// Creates a Dapr streaming subscription for the given subscription.
    /// </summary>
    protected override async Task CreateExternalSubscriptionAsync(ISubscription subscription, CancellationToken cancellationToken)
    {
        // Avoid duplicates
        if (ExternalSubscriptions.ContainsKey(subscription.Id))
        {
            Logger.LogWarning(
                "Dapr subscription already exists for {SubscriptionId}, skipping creation",
                subscription.Id);
            return;
        }

        try
        {
            Logger.LogDebug(
                "Creating Dapr subscription for topic {Topic} (SubscriptionId: {SubscriptionId}, ServiceKey: {ServiceKey})",
                subscription.TopicName, subscription.Id, ServiceKey ?? "default");

            // Message handler function
            async Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken ct)
            {
                try
                {
                    // Deserialize JSON from message data
                    var eventData = JsonSerializer.Deserialize(message.Data.Span, subscription.EventType);
                    if (eventData != null)
                    {
                        // Trigger handlers through EventBus
                        await EventBus.TriggerHandlersAsync(
                            subscription.EventType,
                            eventData,
                            ct);
                    }

                    return TopicResponseAction.Success;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex,
                        "Error handling Dapr message for topic {Topic} (SubscriptionId: {SubscriptionId})",
                        subscription.TopicName, subscription.Id);
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
                subscription.TopicName,
                subscriptionOptions,
                HandleMessageAsync,
                cancellationToken);

            ExternalSubscriptions.TryAdd(subscription.Id, daprSubscription);

            Logger.LogInformation(
                "Created Dapr subscription for topic {Topic} (SubscriptionId: {SubscriptionId}, ServiceKey: {ServiceKey})",
                subscription.TopicName, subscription.Id, ServiceKey ?? "default");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Failed to create Dapr subscription for topic {Topic} (SubscriptionId: {SubscriptionId})",
                subscription.TopicName, subscription.Id);
            throw;
        }
    }
}
