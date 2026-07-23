using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Services.Support;
using Monica.EventBus.Services.Support;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Native Kafka distributed EventBus provider.
/// </summary>
/// <param name="serviceScopeFactory">Creates scopes for event handlers.</param>
/// <param name="eventHandlerInvoker">Invokes resolved event handlers.</param>
/// <param name="subscriptionManager">Owns this host's subscription catalog.</param>
/// <param name="clusterConfigProvider">Provides Kafka cluster settings.</param>
/// <param name="jsonSerializerOptionsProvider">Provides host JSON settings.</param>
/// <param name="options">Provides Kafka event bus options.</param>
/// <param name="loggerFactory">Creates the event bus logger.</param>
/// <param name="serviceKey">An optional keyed-provider identifier.</param>
public sealed class KafkaEventBusProvider(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    IKafkaClusterConfigProvider clusterConfigProvider,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<ModuleEventBusKafkaOption> options,
    ILoggerFactory loggerFactory,
    string? serviceKey = null)
    : DistributedEventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, loggerFactory, serviceKey), IDisposable
{
    // Starting a produce request and disposing the native handle must be mutually exclusive. Once
    // queued, librdkafka owns the delivery and Flush drains it during shutdown.
    private readonly object _producerLifetimeLock = new();
    private readonly Lazy<IProducer<string, string>> _producer = new(() =>
    {
        var cluster = clusterConfigProvider.GetDirectEventBusCluster();
        var config = KafkaClientConfigFactory.BuildProducerConfig(cluster, options.Value);
        return new ProducerBuilder<string, string>(config).Build();
    });
    private bool _isDisposed;

    /// <inheritdoc />
    public override async Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        var payload = JsonSerializer.Serialize(eventData, eventType, jsonSerializerOptionsProvider.SerializerOptions);
        await StartProduce(
            finalTopicName,
            eventType,
            payload,
            cancellationToken);
    }

    /// <inheritdoc />
    public override async Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        foreach (var eventData in eventDataList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(eventData, eventType, jsonSerializerOptionsProvider.SerializerOptions);
            await StartProduce(
                finalTopicName,
                eventType,
                payload,
                cancellationToken);
        }
    }

    private Task<DeliveryResult<string, string>> StartProduce(
        string topicName,
        Type eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        lock (_producerLifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            return _producer.Value.ProduceAsync(
                topicName,
                new Message<string, string>
                {
                    Key = eventType.FullName ?? eventType.Name,
                    Value = payload
                },
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_producerLifetimeLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (!_producer.IsValueCreated)
            {
                return;
            }

            try
            {
                _producer.Value.Flush(options.Value.ProducerFlushTimeout);
            }
            finally
            {
                _producer.Value.Dispose();
            }
        }
    }
}
