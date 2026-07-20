using System.Collections.Concurrent;
using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Dapr.Abstractions;
using Monica.Modules;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Services.Support;

namespace Monica.Dapr.Services;

/// <summary>
/// Dapr-specific implementation of subscription hosted service.
/// Manages Dapr streaming subscriptions for distributed events.
/// Now includes observable state management for monitoring.
/// Runs as a background service that doesn't block application startup.
/// </summary>
internal class DaprEventBusSubscriptionHostedService(
    DaprPublishSubscribeClient daprClient,
    IEventSubscriptionRegistry subscriptionManager, 
    IHostApplicationLifetime applicationLifetime,
    IDistributedEventBus eventBus,
    IObservableInstanceRegistry observableManager,
    IDaprSidecarHealthCoordinator healthCoordinator,
    IOptions<ModuleDaprEventBusOption> options,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<DaprEventBusSubscriptionHostedService> logger,
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
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.EventBus);

    // Track Dapr subscriptions by topic name
    private readonly ConcurrentDictionary<string, IAsyncDisposable> _daprSubscriptionsByTopic = new();
    private bool _wasHealthy;

    /// <summary>
    /// Override ExecuteBackgroundAsync to wait for Dapr sidecar health before creating subscriptions.
    /// This runs in the background without blocking application startup.
    /// </summary>
    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState("Waiting for Dapr sidecar to become healthy", HostedServiceState.Starting);

        Logger.LogInformation("Waiting for Dapr sidecar health check...");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for Dapr sidecar to be healthy before subscribing.
            var isHealthy = await healthCoordinator.WaitForHealthyAsync(
                _options.SidecarHealthWaitTimeout,
                stoppingToken);

            if (isHealthy)
            {
                break;
            }

            var message = "Dapr sidecar is not healthy. Subscription creation will be retried.";
            RecordState(message, HostedServiceState.Degraded);
            Logger.LogWarning(
                "Dapr sidecar is not healthy. Retrying subscription creation in {Delay}",
                _options.SidecarHealthWaitTimeout);

            if (_options.FailFastOnSidecarUnavailable)
            {
                applicationLifetime.StopApplication();
                return;
            }
        }

        stoppingToken.ThrowIfCancellationRequested();

        RecordState("Dapr sidecar is healthy, proceeding with subscription creation", HostedServiceState.Starting);

        Logger.LogInformation("Dapr sidecar is healthy, creating subscriptions");

        _wasHealthy = true;

        // Now safe to create subscriptions - call base to initialize and keep running
        await base.ExecuteBackgroundAsync(stoppingToken);
    }

    protected override async Task OnHeartbeatAsync(CancellationToken cancellationToken)
    {
        await base.OnHeartbeatAsync(cancellationToken);

        if (!healthCoordinator.IsHealthy)
        {
            _wasHealthy = false;
            return;
        }

        if (_wasHealthy)
        {
            return;
        }

        _wasHealthy = true;
        await RecreateExternalSubscriptionsAsync(cancellationToken);
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
            RecordState($"Starting Dapr subscription for topic {topicName}", HostedServiceState.Running);

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

            RecordState($"Successfully created Dapr subscription for topic {topicName}", HostedServiceState.Running);

            Logger.LogInformation(
                "Created Dapr subscription for topic {Topic} with EventType {EventType} (ServiceKey: {ServiceKey})",
                topicName, eventType.Name, ServiceKey ?? "default");
        }
        catch (Exception ex)
        {
            RecordState($"Failed to create Dapr subscription for topic {topicName}",
                HostedServiceState.Degraded, ex);

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
                    jsonSerializerOptionsProvider.SerializerOptions);

                if (eventData == null)
                {
                    RecordState($"Failed to deserialize message for topic {message.Topic}", HostedServiceState.Degraded);

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
                RecordState($"Error handling Dapr message for topic {message.Topic}",
                    HostedServiceState.Degraded, ex);

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
                RecordState($"Disposing Dapr subscription for topic {topicName}", HostedServiceState.Running);

                Logger.LogInformation(
                    "Disposing Dapr subscription for topic {Topic} (ServiceKey: {ServiceKey})",
                    topicName, ServiceKey ?? "default");

                await daprSubscription.DisposeAsync();

                RecordState($"Successfully disposed Dapr subscription for topic {topicName}", HostedServiceState.Running);
            }
            catch (Exception ex)
            {
                RecordState($"Error disposing Dapr subscription for topic {topicName}",
                    HostedServiceState.Degraded, ex);

                Logger.LogError(ex,
                    "Error disposing Dapr subscription for topic {Topic}",
                    topicName);
                throw;
            }
        }
        else
        {
            RecordState($"Attempted to remove non-existent Dapr subscription for topic {topicName}", HostedServiceState.Degraded);

            Logger.LogWarning(
                "Attempted to remove Dapr subscription for topic {Topic}, but it was not found",
                topicName);
        }
    }

    protected override async Task DisposeExternalSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var subscriptions = _daprSubscriptionsByTopic.ToArray();
        _daprSubscriptionsByTopic.Clear();

        foreach (var (topicName, subscription) in subscriptions)
        {
            try
            {
                Logger.LogInformation(
                    "Disposing Dapr subscription for topic {Topic} during shutdown (ServiceKey: {ServiceKey})",
                    topicName,
                    ServiceKey ?? "default");

                await subscription.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error disposing Dapr subscription for topic {Topic} during shutdown",
                    topicName);
            }
        }
    }
}
