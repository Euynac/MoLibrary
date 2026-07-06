namespace Monica.EventBus.Kafka.Models;

/// <summary>
/// Current Kafka EventBus integration state displayed by the console.
/// </summary>
public sealed class KafkaIntegrationSnapshot
{
    /// <summary>
    /// Resolved integration mode.
    /// </summary>
    public KafkaIntegrationMode Mode { get; set; }

    /// <summary>
    /// Display name for the active distributed EventBus provider.
    /// </summary>
    public string ActiveProviderName { get; set; } = "Unknown";

    /// <summary>
    /// Dapr pub/sub component name when Dapr-backed Kafka is configured.
    /// </summary>
    public string? DaprPubSubName { get; set; }

    /// <summary>
    /// Cluster id selected as the primary Kafka integration target.
    /// </summary>
    public string? PrimaryClusterId { get; set; }

    /// <summary>
    /// Console capabilities available for the active integration.
    /// </summary>
    public KafkaConsoleCapabilities Capabilities { get; set; }

    /// <summary>
    /// Human-readable details explaining unavailable or degraded capabilities.
    /// </summary>
    public IReadOnlyList<string> Messages { get; set; } = [];
}

/// <summary>
/// Summarizes a Kafka cluster and its last known console state.
/// </summary>
public sealed class KafkaClusterSummary
{
    /// <summary>
    /// Cluster configuration.
    /// </summary>
    public KafkaClusterConfig Config { get; set; } = new();

    /// <summary>
    /// Whether the last connection probe succeeded.
    /// </summary>
    public bool IsReachable { get; set; }

    /// <summary>
    /// Last connection or sampling error, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Broker count from the last metadata query.
    /// </summary>
    public int BrokerCount { get; set; }

    /// <summary>
    /// Topic count from the last metadata query.
    /// </summary>
    public int TopicCount { get; set; }

    /// <summary>
    /// Consumer group count from the last group query.
    /// </summary>
    public int ConsumerGroupCount { get; set; }

    /// <summary>
    /// Time when the summary was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Kafka broker metadata.
/// </summary>
public sealed class KafkaBrokerInfo
{
    /// <summary>
    /// Broker id assigned by Kafka.
    /// </summary>
    public int BrokerId { get; set; }

    /// <summary>
    /// Broker host name or address.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Broker port.
    /// </summary>
    public int Port { get; set; }
}

/// <summary>
/// Kafka topic summary displayed in topic management.
/// </summary>
public sealed class KafkaTopicSummary
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Number of partitions in the topic.
    /// </summary>
    public int Partitions { get; set; }

    /// <summary>
    /// Best-effort replication factor calculated from partition metadata.
    /// </summary>
    public int ReplicationFactor { get; set; }

    /// <summary>
    /// Whether the topic is an internal Kafka topic.
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Optional retention time in milliseconds.
    /// </summary>
    public long? RetentionMs { get; set; }
}

/// <summary>
/// Request for creating a Kafka topic.
/// </summary>
public sealed class KafkaTopicCreateRequest
{
    /// <summary>
    /// Target cluster id.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Number of partitions to create.
    /// </summary>
    public int Partitions { get; set; } = 1;

    /// <summary>
    /// Replication factor to request.
    /// </summary>
    public short ReplicationFactor { get; set; } = 1;

    /// <summary>
    /// Optional retention time in milliseconds.
    /// </summary>
    public long? RetentionMs { get; set; }
}

/// <summary>
/// Request for increasing a topic's partition count.
/// </summary>
public sealed class KafkaTopicPartitionRequest
{
    /// <summary>
    /// Target cluster id.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// New total partition count. Kafka does not allow reducing partitions.
    /// </summary>
    public int IncreaseTo { get; set; }
}

/// <summary>
/// Request for updating topic retention.
/// </summary>
public sealed class KafkaTopicRetentionRequest
{
    /// <summary>
    /// Target cluster id.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Retention time in milliseconds.
    /// </summary>
    public long RetentionMs { get; set; }
}

/// <summary>
/// Request for reading a bounded sample of topic messages without committing consumer offsets.
/// </summary>
public sealed class KafkaTopicMessagesRequest
{
    /// <summary>
    /// Target cluster id.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of messages to return. The module clamps this to its configured preview limit.
    /// </summary>
    public int MaxMessages { get; set; } = 20;
}

/// <summary>
/// Bounded topic message sample returned to the Kafka console.
/// </summary>
public sealed class KafkaTopicMessageBatch
{
    /// <summary>
    /// Target cluster id.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the preview started from each partition's latest offsets.
    /// </summary>
    public bool ReadFromEnd { get; set; } = true;

    /// <summary>
    /// Maximum number of messages requested after module-level clamping.
    /// </summary>
    public int MaxMessages { get; set; }

    /// <summary>
    /// Time when the sample was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Sampled messages ordered from newest to oldest.
    /// </summary>
    public IReadOnlyList<KafkaTopicMessageSample> Messages { get; set; } = [];
}

/// <summary>
/// Kafka message metadata and decoded content shown in the console.
/// </summary>
public sealed class KafkaTopicMessageSample
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Partition that contains the message.
    /// </summary>
    public int Partition { get; set; }

    /// <summary>
    /// Kafka offset within the partition.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// Producer timestamp when Kafka exposes it.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Decoded message key, if present.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Decoded message value, base64 payload, or null for tombstone messages.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Encoding used for <see cref="Value"/>.
    /// </summary>
    public string ValueEncoding { get; set; } = "utf-8";

    /// <summary>
    /// Original value payload length in bytes.
    /// </summary>
    public int ValueSizeBytes { get; set; }

    /// <summary>
    /// Whether the value was truncated before display.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Whether the Kafka value is null, which represents a compacted-topic tombstone.
    /// </summary>
    public bool IsTombstone { get; set; }
}

/// <summary>
/// Consumer group summary displayed in the console.
/// </summary>
public sealed class KafkaConsumerGroupSummary
{
    /// <summary>
    /// Consumer group id.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Current group state reported by Kafka.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Member count from consumer group description.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Total lag when it can be calculated; otherwise null.
    /// </summary>
    public long? TotalLag { get; set; }
}

/// <summary>
/// Cluster performance snapshot retained by the Kafka console.
/// </summary>
public sealed class KafkaPerformanceSnapshot
{
    /// <summary>
    /// Target cluster id.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// UTC capture time.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Broker count.
    /// </summary>
    public int BrokerCount { get; set; }

    /// <summary>
    /// Topic count.
    /// </summary>
    public int TopicCount { get; set; }

    /// <summary>
    /// Consumer group count.
    /// </summary>
    public int ConsumerGroupCount { get; set; }

    /// <summary>
    /// Total consumer lag when it can be calculated.
    /// </summary>
    public long? TotalLag { get; set; }

    /// <summary>
    /// Whether JMX metrics were included in this snapshot.
    /// </summary>
    public bool IncludesJmxMetrics { get; set; }

    /// <summary>
    /// Optional diagnostic message recorded during sampling.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Dashboard aggregate for the selected Kafka cluster.
/// </summary>
public sealed class KafkaDashboardSnapshot
{
    /// <summary>
    /// Current integration state.
    /// </summary>
    public KafkaIntegrationSnapshot Integration { get; set; } = new();

    /// <summary>
    /// Selected cluster summary.
    /// </summary>
    public KafkaClusterSummary? SelectedCluster { get; set; }

    /// <summary>
    /// Latest performance snapshot for the selected cluster.
    /// </summary>
    public KafkaPerformanceSnapshot? LatestPerformance { get; set; }

    /// <summary>
    /// Topic count shown by the dashboard.
    /// </summary>
    public int TopicCount { get; set; }

    /// <summary>
    /// Consumer group count shown by the dashboard.
    /// </summary>
    public int ConsumerGroupCount { get; set; }

    /// <summary>
    /// Broker count shown by the dashboard.
    /// </summary>
    public int BrokerCount { get; set; }
}
