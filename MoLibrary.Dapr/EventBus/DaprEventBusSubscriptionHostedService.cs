using System.Collections.Concurrent;
using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Modules;
using MoLibrary.Dapr.Interfaces;
using MoLibrary.Dapr.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Services;

namespace MoLibrary.Dapr.EventBus;

/// <summary>
/// Dapr-specific implementation of subscription hosted service.
/// Manages Dapr streaming subscriptions for distributed events.
/// Now includes observable state management for monitoring.
/// </summary>
internal class DaprEventBusSubscriptionHostedService(
    DaprPublishSubscribeClient daprClient,
    ISubscriptionManager subscriptionManager, 
    IHostApplicationLifetime applicationLifetime,
    IMoDistributedEventBus eventBus,
    IObservableInstanceManager observableManager,
    IDaprSidecarHealthCoordinator healthCoordinator,
    IOptions<ModuleDaprEventBusOption> options,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<DaprEventBusSubscriptionHostedService> logger,
    IGlobalJsonOption jsonOption,
    string? serviceKey = null)
    : EventBusSubscriptionHostedServiceBase(
        subscriptionManager,
        eventBus,
        observableManager,
        hostedServiceOptions,
        logger,
        serviceKey)
{
    private readonly ModuleDaprEventBusOption _options = options.Value;

    /// <summary>
    /// Gets the name of this service for identification and monitoring
    /// </summary>
    public override string ServiceName => $"DaprEventBus{(ServiceKey != null ? $"_{ServiceKey}" : "")}";

    // Track Dapr subscriptions by topic name
    private readonly ConcurrentDictionary<string, IAsyncDisposable> _daprSubscriptionsByTopic = new();

    /// <summary>
    /// Override OnStartingAsync to wait for Dapr sidecar health before creating subscriptions.
    /// </summary>
    protected override async Task OnStartingAsync(CancellationToken cancellationToken)
    {
        RecordStateChange(
            HostedServiceState.Starting,
            "Waiting for Dapr sidecar to become healthy");

        Logger.LogInformation("Waiting for Dapr sidecar health check...");

        // Wait for Dapr sidecar to be healthy before subscribing
        var isHealthy = await healthCoordinator.WaitForHealthyAsync(
            _options.SidecarHealthWaitTimeout,
            cancellationToken);

        if (!isHealthy)
        {
            var message = "Dapr sidecar is not healthy. Subscription creation will be skipped.";
            RecordStateChange(HostedServiceState.Degraded, message);
            Logger.LogWarning(message);

            if (_options.FailFastOnSidecarUnavailable)
            {
                applicationLifetime.StopApplication();
            }
            
            return; // Don't call base - skip subscription creation
        }

        RecordStateChange(
            HostedServiceState.Starting,
            "Dapr sidecar is healthy, proceeding with subscription creation");

        Logger.LogInformation("Dapr sidecar is healthy, creating subscriptions");

        // Now safe to create subscriptions
        await base.OnStartingAsync(cancellationToken);
    }

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
            RecordStateChange(
                HostedServiceState.Running,
                $"Starting Dapr subscription for topic {topicName}");

            Logger.LogDebug(
                "Creating Dapr subscription for topic {Topic} with EventType {EventType} (ServiceKey: {ServiceKey})",
                topicName, eventType.Name, ServiceKey ?? "default");

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

            RecordStateChange(
                HostedServiceState.Running,
                $"Successfully created Dapr subscription for topic {topicName}");

            Logger.LogInformation(
                "Created Dapr subscription for topic {Topic} with EventType {EventType} (ServiceKey: {ServiceKey})",
                topicName, eventType.Name, ServiceKey ?? "default");
        }
        catch (Exception ex)
        {
            RecordStateChange(
                HostedServiceState.Degraded,
                $"Failed to create Dapr subscription for topic {topicName}",
                ex);

            Logger.LogError(ex,
                "Failed to create Dapr subscription for topic {Topic}",
                topicName);
            throw;
        }

        return;

        // Message handler - deserializes and delegates to base class
        async Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken ct)
        {
            try
            {
                // Debug logging for raw message data
                if (_options.EnableMessageDataDebugLogging)
                {
                    var rawJson = System.Text.Encoding.UTF8.GetString(message.Data.Span);
                    Logger.LogInformation(
                        "Received message on topic {Topic}: {RawJson}",
                        message.Topic, rawJson);
                }

                // Deserialize message data using the topic's event type
                var eventData = JsonSerializer.Deserialize(
                    message.Data.Span,
                    eventType,
                    jsonOption.GlobalOptions);

                if (eventData == null)
                {
                    RecordStateChange(
                        HostedServiceState.Degraded,
                        $"Failed to deserialize message for topic {message.Topic}");

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
                RecordStateChange(
                    HostedServiceState.Degraded,
                    $"Error handling Dapr message for topic {message.Topic}",
                    ex);

                Logger.LogError(ex,
                    "Error handling Dapr message for topic {Topic}",
                    message.Topic);
                return TopicResponseAction.Drop;
            }
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
                RecordStateChange(
                    HostedServiceState.Running,
                    $"Disposing Dapr subscription for topic {topicName}");

                Logger.LogInformation(
                    "Disposing Dapr subscription for topic {Topic} (ServiceKey: {ServiceKey})",
                    topicName, ServiceKey ?? "default");

                await daprSubscription.DisposeAsync();

                RecordStateChange(
                    HostedServiceState.Running,
                    $"Successfully disposed Dapr subscription for topic {topicName}");
            }
            catch (Exception ex)
            {
                RecordStateChange(
                    HostedServiceState.Degraded,
                    $"Error disposing Dapr subscription for topic {topicName}",
                    ex);

                Logger.LogError(ex,
                    "Error disposing Dapr subscription for topic {Topic}",
                    topicName);
                throw;
            }
        }
        else
        {
            RecordStateChange(
                HostedServiceState.Degraded,
                $"Attempted to remove non-existent Dapr subscription for topic {topicName}");

            Logger.LogWarning(
                "Attempted to remove Dapr subscription for topic {Topic}, but it was not found",
                topicName);
        }
    }
}
