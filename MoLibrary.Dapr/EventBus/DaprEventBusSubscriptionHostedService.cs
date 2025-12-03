using System.Text;
using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Dapr.Modules;

namespace MoLibrary.Dapr.EventBus;

/// <summary>
/// Background service that manages streaming subscriptions for Dapr EventBus.
/// Subscribes to all distributed event topics and processes messages.
/// </summary>
public class DaprEventBusSubscriptionHostedService(
    DaprPublishSubscribeClient messagingClient,
    DistributedEventBusDaprProvider eventBus,
    IGlobalJsonOption jsonOption,
    IOptions<ModuleDaprEventBusOption> options,
    ILogger<DaprEventBusSubscriptionHostedService> logger) : BackgroundService
{
    private readonly ModuleDaprEventBusOption _options = options.Value;
    private readonly List<IAsyncDisposable> _subscriptions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 1. Get distributed handlers from pre-computed metadata
            var distributedHandlers = eventBus.GetAutoRegisteredHandlers();

            // 2. Group by topic (multiple handlers can share same topic)
            var topicGroups = distributedHandlers.GroupBy(h => h.TopicName);

            // 3. Subscribe to each unique topic
            foreach (var topicGroup in topicGroups)
            {
                var topic = topicGroup.Key;

                // Create subscription options
                var subscriptionOptions = new DaprSubscriptionOptions(
                    new MessageHandlingPolicy(
                        _options.MessageHandlingTimeout,
                        TopicResponseAction.Retry
                    ))
                {
                    MaximumQueuedMessages = _options.MaximumQueuedMessages,
                    MaximumCleanupTimeout = _options.MaximumCleanupTimeout,
                    DeadLetterTopic = _options.DeadLetterTopic
                };

                // Subscribe to topic
                var subscription = await messagingClient.SubscribeAsync(
                    _options.PubSubName,
                    topic,
                    subscriptionOptions,
                    HandleMessageAsync,
                    stoppingToken
                );

                _subscriptions.Add(subscription);

                logger.LogInformation(
                    "Subscribed to topic {Topic} on pubsub {PubSubName}",
                    topic, _options.PubSubName);
            }

            logger.LogInformation(
                "Dapr EventBus: {Count} streaming subscriptions active",
                _subscriptions.Count);

            // 4. Keep service alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown - no need to log
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing Dapr EventBus subscriptions");
            throw;
        }
    }

    /// <summary>
    /// Handles incoming messages from Dapr streaming subscriptions.
    /// Deserializes messages and triggers registered event handlers.
    /// </summary>
    private Task<TopicResponseAction> HandleMessageAsync(
        TopicMessage message,
        CancellationToken cancellationToken)
    {
        var topic = message.Topic;

        try
        {
            // Get event type from pre-computed mapping
            var eventType = eventBus.GetEventType(topic);

            // Deserialize using global JSON options
            var data = Encoding.UTF8.GetString(message.Data.Span);
            var eventData = JsonSerializer.Deserialize(data, eventType, jsonOption.GlobalOptions);

            if (eventData == null)
            {
                logger.LogError(
                    "Event deserialization returned null for topic {Topic}, data: {Data}",
                    topic, data);
                return Task.FromResult(TopicResponseAction.Drop);
            }

            // CRITICAL: Synchronously wait for handlers to complete
            // Must ensure Success is returned only after handler completion
            eventBus.TriggerHandlersAsync(eventType, eventData).GetAwaiter().GetResult();

            return Task.FromResult(TopicResponseAction.Success);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Event name"))
        {
            // Event type not found - configuration mismatch
            logger.LogError(ex, "Unknown event type for topic {Topic}", topic);
            return Task.FromResult(TopicResponseAction.Drop);
        }
        catch (JsonException ex)
        {
            // Deserialization failed - malformed message
            logger.LogError(ex, "JSON deserialization failed for topic {Topic}", topic);
            return Task.FromResult(TopicResponseAction.Drop);
        }
        catch (Exception ex)
        {
            // Handler execution error - likely transient
            logger.LogError(ex, "Error handling message for topic {Topic}", topic);
            return Task.FromResult(TopicResponseAction.Retry);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Dapr EventBus subscriptions...");

        await base.StopAsync(cancellationToken);

        // Dispose all subscriptions (triggers graceful cleanup with queue processing)
        foreach (var subscription in _subscriptions)
        {
            try
            {
                await subscription.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error disposing subscription");
            }
        }

        logger.LogInformation("All {Count} subscriptions disposed", _subscriptions.Count);
    }
}
