namespace Monica.EventBus.Kafka.Models;

/// <summary>
/// Describes how Monica is currently integrated with Kafka.
/// </summary>
public enum KafkaIntegrationMode
{
    /// <summary>
    /// No Kafka integration has been configured.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// Monica uses the native Kafka provider as the distributed EventBus provider.
    /// </summary>
    DirectKafka,

    /// <summary>
    /// Monica uses Dapr as the EventBus provider and the configured Dapr pub/sub component is backed by Kafka.
    /// </summary>
    DaprKafka,

    /// <summary>
    /// Monica uses Dapr as the EventBus provider, but no Kafka-backed component is declared.
    /// </summary>
    DaprNonKafka,

    /// <summary>
    /// Monica cannot determine the active distributed EventBus provider.
    /// </summary>
    Unknown
}

/// <summary>
/// Kafka console capability flags for the selected integration mode and cluster configuration.
/// </summary>
[Flags]
public enum KafkaConsoleCapabilities
{
    /// <summary>
    /// No Kafka console capabilities are available.
    /// </summary>
    None = 0,

    /// <summary>
    /// The configuration can publish and subscribe to EventBus topics.
    /// </summary>
    PublishSubscribe = 1 << 0,

    /// <summary>
    /// The console can execute Kafka topic administration operations.
    /// </summary>
    TopicAdmin = 1 << 1,

    /// <summary>
    /// The console can inspect consumer groups.
    /// </summary>
    ConsumerGroups = 1 << 2,

    /// <summary>
    /// The console can collect broker, topic, and consumer group snapshots.
    /// </summary>
    PerformanceSampling = 1 << 3,

    /// <summary>
    /// The console can include broker JMX metrics.
    /// </summary>
    JmxMetrics = 1 << 4
}
