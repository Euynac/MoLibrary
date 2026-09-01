using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Models;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Services.Support;
using Monica.EventBus.Models;
using Monica.EventBus.Services.Support;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Hosted service that owns Kafka consumers for active distributed EventBus subscriptions.
/// </summary>
internal sealed class KafkaEventBusSubscriptionHostedService(
    IEventSubscriptionRegistry subscriptionManager,
    IDistributedEventBus eventBus,
    ITopicSubscriptionStatusStore topicStatusStore,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory,
    IKafkaClusterConfigProvider clusterConfigProvider,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<ModuleEventBusKafkaOption> options,
    ILogger<KafkaEventBusSubscriptionHostedService> logger,
    string? serviceKey = null)
    : EventBusSubscriptionHostedServiceBase(
        subscriptionManager,
        eventBus,
        topicStatusStore,
        observableManager,
        hostedServiceOptions,
        serviceScopeFactory,
        logger,
        serviceKey)
{
    private readonly TopicConsumerRegistry _consumers = new();

    /// <inheritdoc />
    public override string ServiceName => $"KafkaEventBus{(ServiceKey is null ? string.Empty : $"_{ServiceKey}")}";

    /// <inheritdoc />
    public override string? ServiceGroupId => nameof(ModuleEventBus);

    /// <inheritdoc />
    protected override EventBusProviderKind ProviderKind => EventBusProviderKind.Kafka;

    protected override Task CreateExternalSubscriptionForTopicAsync(string topicName, Type eventType, CancellationToken cancellationToken)
    {
        var consumer = new TopicConsumer(topicName, eventType, cancellationToken, ConsumeAsync);
        if (!_consumers.TryAddAndStart(consumer))
        {
            return consumer.StopAsync();
        }

        RecordState($"Started Kafka consumer for topic {topicName}", HostedServiceState.Running);
        return Task.CompletedTask;
    }

    protected override async Task RemoveExternalSubscriptionForTopicAsync(string topicName, CancellationToken cancellationToken)
    {
        var consumer = _consumers.Remove(topicName);
        if (consumer is null)
        {
            return;
        }

        await consumer.StopAsync();
    }

    protected override Task DisposeExternalSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var stopTasks = _consumers.Drain().Select(consumer => consumer.StopAsync());
        return Task.WhenAll(stopTasks);
    }

    protected override Task OnStoppingAsync(CancellationToken cancellationToken)
    {
        _consumers.StopAccepting();
        return base.OnStoppingAsync(cancellationToken);
    }

    private async Task ConsumeAsync(TopicConsumer topicConsumer)
    {
        try
        {
            await ConsumeTopicAsync(topicConsumer);
        }
        catch (OperationCanceledException)
        {
            // Expected during unsubscribe and shutdown.
        }
        catch (Exception ex)
        {
            TopicStatusStore.ReportState(
                ServiceKey, topicConsumer.TopicName, ProviderKind, TopicSubscriptionRuntimeState.Failed,
                $"Kafka consumer loop terminated for topic {topicConsumer.TopicName}; " +
                "the topic will not receive messages until the application restarts it",
                ex);

            RecordState($"Kafka consumer stopped for topic {topicConsumer.TopicName}", HostedServiceState.Degraded, ex);
        }
    }

    private async Task ConsumeTopicAsync(TopicConsumer topicConsumer)
    {
        var cancellationToken = topicConsumer.CancellationToken;
        var cluster = clusterConfigProvider.GetDirectEventBusCluster();
        var consumerConfig = KafkaClientConfigFactory.BuildConsumerConfig(cluster, options.Value, ServiceKey);
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topicConsumer.TopicName);

        // The consumer proves health only by delivering its first message; assignment events are
        // not observable through the Confluent consumer API used here.
        var hasProvenHealthy = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(cancellationToken);
                if (result?.Message?.Value is null)
                {
                    continue;
                }

                var eventData = JsonSerializer.Deserialize(
                    result.Message.Value,
                    topicConsumer.EventType,
                    jsonSerializerOptionsProvider.SerializerOptions);
                if (eventData is null)
                {
                    TopicStatusStore.ReportError(
                        ServiceKey, topicConsumer.TopicName,
                        $"Kafka message on topic {topicConsumer.TopicName} deserialized to null");
                    RecordState($"Kafka message on topic {topicConsumer.TopicName} deserialized to null", HostedServiceState.Degraded);
                    continue;
                }

                await HandleExternalMessageAsync(
                    topicConsumer.TopicName,
                    eventData,
                    cancellationToken);

                TopicStatusStore.ReportMessageProcessed(ServiceKey, topicConsumer.TopicName);

                if (!hasProvenHealthy)
                {
                    hasProvenHealthy = true;
                    TopicStatusStore.ReportState(
                        ServiceKey, topicConsumer.TopicName, ProviderKind, TopicSubscriptionRuntimeState.Healthy,
                        $"Kafka consumer is delivering messages for topic {topicConsumer.TopicName}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                TopicStatusStore.ReportState(
                    ServiceKey, topicConsumer.TopicName, ProviderKind, TopicSubscriptionRuntimeState.Recovering,
                    $"Kafka consumer failed for topic {topicConsumer.TopicName}; retrying after backoff",
                    ex);

                RecordState($"Kafka consumer failed for topic {topicConsumer.TopicName}", HostedServiceState.Degraded, ex);
                try
                {
                    await Task.Delay(options.Value.ConsumerErrorBackoff, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        try
        {
            consumer.Commit();
        }
        catch (KafkaException ex)
        {
            TopicStatusStore.ReportError(
                ServiceKey, topicConsumer.TopicName,
                $"Kafka consumer final offset commit failed for topic {topicConsumer.TopicName}",
                ex);
            RecordState(
                $"Kafka consumer final offset commit failed for topic {topicConsumer.TopicName}",
                HostedServiceState.Degraded,
                ex);
        }

        // Consumer.Close enters a librdkafka LeaveGroup path that can dereference a missing
        // coordinator. Consumer.Dispose uses NO_CONSUMER_CLOSE instead.
    }

    /// <summary>
    /// Owns publication and draining of topic consumers across service lifecycle transitions.
    /// </summary>
    internal sealed class TopicConsumerRegistry
    {
        private readonly object _lifecycleLock = new();
        private readonly Dictionary<string, TopicConsumer> _consumers = new(StringComparer.Ordinal);
        private bool _isAccepting = true;

        internal bool TryAddAndStart(TopicConsumer consumer)
        {
            lock (_lifecycleLock)
            {
                if (!_isAccepting || !_consumers.TryAdd(consumer.TopicName, consumer))
                {
                    return false;
                }

                if (consumer.TryStart())
                {
                    return true;
                }

                _consumers.Remove(consumer.TopicName);
                return false;
            }
        }

        internal TopicConsumer? Remove(string topicName)
        {
            lock (_lifecycleLock)
            {
                return _consumers.Remove(topicName, out var consumer)
                    ? consumer
                    : null;
            }
        }

        internal IReadOnlyList<TopicConsumer> Drain()
        {
            lock (_lifecycleLock)
            {
                var consumers = _consumers.Values.ToList();
                _consumers.Clear();
                return consumers;
            }
        }

        internal void StopAccepting()
        {
            lock (_lifecycleLock)
            {
                _isAccepting = false;
            }
        }
    }

    /// <summary>
    /// Owns the execution and cancellation lifetime of one topic consumer.
    /// </summary>
    internal sealed class TopicConsumer
    {
        private readonly object _lifecycleLock = new();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Func<TopicConsumer, Task> _consumeAsync;
        private Task? _consumerTask;
        private Task? _stopTask;

        internal TopicConsumer(
            string topicName,
            Type eventType,
            CancellationToken cancellationToken,
            Func<TopicConsumer, Task> consumeAsync)
        {
            TopicName = topicName;
            EventType = eventType;
            _consumeAsync = consumeAsync;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken = _cancellationTokenSource.Token;
        }

        public string TopicName { get; }

        public Type EventType { get; }

        public CancellationToken CancellationToken { get; }

        internal bool TryStart()
        {
            lock (_lifecycleLock)
            {
                if (_consumerTask is not null || _stopTask is not null)
                {
                    return false;
                }

                _consumerTask = Task.Factory.StartNew(
                    () => _consumeAsync(this),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();
                return true;
            }
        }

        internal Task StopAsync()
        {
            lock (_lifecycleLock)
            {
                // Registry publication can race with removal, so shutdown must capture startup atomically.
                return _stopTask ??= StopCoreAsync(_consumerTask);
            }
        }

        private async Task StopCoreAsync(Task? consumerTask)
        {
            try
            {
                await _cancellationTokenSource.CancelAsync();
            }
            finally
            {
                try
                {
                    if (consumerTask is not null)
                    {
                        await consumerTask;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during unsubscribe and shutdown.
                }
                finally
                {
                    _cancellationTokenSource.Dispose();
                }
            }
        }
    }
}
