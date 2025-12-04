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
internal class DaprEventBusSubscriptionHostedService : EventBusSubscriptionHostedServiceBase
{
    private readonly DaprPublishSubscribeClient _daprClient;
    private readonly ModuleDaprEventBusOption _options;

    public DaprEventBusSubscriptionHostedService(
        DaprPublishSubscribeClient daprClient,
        ISubscriptionManager subscriptionManager,
        IMoEventBus eventBus,
        IOptions<ModuleDaprEventBusOption> options,
        ILogger<DaprEventBusSubscriptionHostedService> logger,
        string? serviceKey = null)
        : base(subscriptionManager, eventBus, logger, serviceKey)
    {
        _daprClient = daprClient;
        _options = options.Value;
    }

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
            Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken ct)
            {
                try
                {
                    // Deserialize JSON from message data
                    var eventData = JsonSerializer.Deserialize(message.Data.Span, subscription.EventType);
                    if (eventData != null)
                    {
                        // Trigger handlers through EventBus
                        EventBus.TriggerHandlersAsync(
                            subscription.EventType,
                            eventData,
                            ct).GetAwaiter().GetResult();
                    }

                    return Task.FromResult(TopicResponseAction.Success);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex,
                        "Error handling Dapr message for topic {Topic} (SubscriptionId: {SubscriptionId})",
                        subscription.TopicName, subscription.Id);
                    return Task.FromResult(TopicResponseAction.Retry);
                }
            }

            // Create Dapr streaming subscription
            var subscriptionOptions = new DaprSubscriptionOptions(
                new MessageHandlingPolicy(_options.MessageHandlingTimeout, TopicResponseAction.Retry));

            var daprSubscription = await _daprClient.SubscribeAsync(
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
