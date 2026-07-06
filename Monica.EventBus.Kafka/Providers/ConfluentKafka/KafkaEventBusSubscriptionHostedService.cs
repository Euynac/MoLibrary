using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Models;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Services.Support;
using Monica.EventBus.Services.Support;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Hosted service that owns Kafka consumers for active distributed EventBus subscriptions.
/// </summary>
internal sealed class KafkaEventBusSubscriptionHostedService(
    IEventSubscriptionRegistry subscriptionManager,
    IDistributedEventBus eventBus,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IKafkaClusterConfigProvider clusterConfigProvider,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<ModuleEventBusKafkaOption> options,
    string? serviceKey = null)
    : EventBusSubscriptionHostedServiceBase(
        subscriptionManager,
        eventBus,
        observableManager,
        hostedServiceOptions,
        serviceKey)
{
    private readonly ConcurrentDictionary<string, TopicConsumer> _consumers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public override string ServiceName => $"KafkaEventBus{(ServiceKey is null ? string.Empty : $"_{ServiceKey}")}";

    /// <inheritdoc />
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.EventBus);

    protected override Task CreateExternalSubscriptionForTopicAsync(string topicName, Type eventType, CancellationToken cancellationToken)
    {
        var consumer = new TopicConsumer(topicName, eventType, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        if (!_consumers.TryAdd(topicName, consumer))
        {
            return Task.CompletedTask;
        }

        consumer.Task = Task.Run(() => ConsumeAsync(consumer), CancellationToken.None);
        RecordState($"Started Kafka consumer for topic {topicName}", HostedServiceState.Running);
        return Task.CompletedTask;
    }

    protected override async Task RemoveExternalSubscriptionForTopicAsync(string topicName, CancellationToken cancellationToken)
    {
        if (!_consumers.TryRemove(topicName, out var consumer))
        {
            return;
        }

        await StopConsumerAsync(consumer);
    }

    protected override async Task DisposeExternalSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var consumers = _consumers.Values.ToList();
        _consumers.Clear();

        foreach (var consumer in consumers)
        {
            await StopConsumerAsync(consumer);
        }
    }

    private async Task StopConsumerAsync(TopicConsumer consumer)
    {
        await consumer.CancellationTokenSource.CancelAsync();
        try
        {
            if (consumer.Task is not null)
            {
                await consumer.Task;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during unsubscribe and shutdown.
        }
        finally
        {
            consumer.CancellationTokenSource.Dispose();
        }
    }

    private async Task ConsumeAsync(TopicConsumer topicConsumer)
    {
        var cluster = clusterConfigProvider.GetDirectEventBusCluster();
        var consumerConfig = KafkaClientConfigFactory.BuildConsumerConfig(cluster, options.Value, ServiceKey);

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topicConsumer.TopicName);

        while (!topicConsumer.CancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(topicConsumer.CancellationTokenSource.Token);
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
                    RecordState($"Kafka message on topic {topicConsumer.TopicName} deserialized to null", HostedServiceState.Degraded);
                    continue;
                }

                await HandleExternalMessageAsync(
                    topicConsumer.TopicName,
                    eventData,
                    topicConsumer.CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordState($"Kafka consumer failed for topic {topicConsumer.TopicName}", HostedServiceState.Degraded, ex);
                await Task.Delay(options.Value.ConsumerErrorBackoff, topicConsumer.CancellationTokenSource.Token);
            }
        }

        consumer.Close();
    }

    private sealed class TopicConsumer(string topicName, Type eventType, CancellationTokenSource cancellationTokenSource)
    {
        public string TopicName { get; } = topicName;

        public Type EventType { get; } = eventType;

        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public Task? Task { get; set; }
    }
}
