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
    private readonly Lazy<IProducer<string, string>> _producer = new(() =>
    {
        var cluster = clusterConfigProvider.GetDirectEventBusCluster();
        var config = KafkaClientConfigFactory.BuildProducerConfig(cluster, options.Value);
        return new ProducerBuilder<string, string>(config).Build();
    });

    /// <inheritdoc />
    public override async Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        var payload = JsonSerializer.Serialize(eventData, eventType, jsonSerializerOptionsProvider.SerializerOptions);
        await _producer.Value.ProduceAsync(
            finalTopicName,
            new Message<string, string>
            {
                Key = eventType.FullName ?? eventType.Name,
                Value = payload
            },
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
            await _producer.Value.ProduceAsync(
                finalTopicName,
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
        if (!_producer.IsValueCreated)
        {
            return;
        }

        _producer.Value.Flush(options.Value.ProducerFlushTimeout);
        _producer.Value.Dispose();
    }
}
