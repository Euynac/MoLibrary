namespace Monica.EventBus.Kafka.Models;

/// <summary>
/// Provider-neutral description of a Kafka consumer group and its live assignments.
/// </summary>
public sealed class KafkaConsumerGroupDescription
{
    /// <summary>
    /// Consumer group identifier.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Current state reported by Kafka.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Provider diagnostic when the group description or metric query was incomplete; otherwise null.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Members currently registered in the group.
    /// </summary>
    public IReadOnlyList<KafkaConsumerGroupMemberAssignment> Members { get; set; } = [];
}

/// <summary>
/// Identity and partition assignment of one consumer-group member.
/// </summary>
public sealed class KafkaConsumerGroupMemberAssignment
{
    /// <summary>
    /// Broker-generated consumer identifier.
    /// </summary>
    public string ConsumerId { get; set; } = string.Empty;

    /// <summary>
    /// Host reported by the consumer.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Client identifier reported by the consumer.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Topic partitions assigned to this member at capture time.
    /// </summary>
    public IReadOnlyList<KafkaConsumerPartitionAssignment> Partitions { get; set; } = [];
}

/// <summary>
/// A single topic-partition assignment in a consumer group.
/// </summary>
public sealed class KafkaConsumerPartitionAssignment
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Partition number.
    /// </summary>
    public int Partition { get; set; }
}

/// <summary>
/// A captured consumer-group view for one topic.
/// </summary>
public sealed class KafkaConsumerGroupTopicMetrics
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Consumer group identifier.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Group state at capture time.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// UTC capture time.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Sum of current offsets for the topic and group.
    /// </summary>
    public long? TotalCurrentOffset { get; set; }

    /// <summary>
    /// Sum of log-end offsets for the topic's partitions.
    /// </summary>
    public long? TotalLogEndOffset { get; set; }

    /// <summary>
    /// Total messages currently retained by the topic's partition offset ranges.
    /// </summary>
    public long? TotalRetainedMessageCount { get; set; }

    /// <summary>
    /// Sum of partition lag for the topic and group.
    /// </summary>
    public long? TotalLag { get; set; }

    /// <summary>
    /// Estimated consumer rate for this topic and group.
    /// </summary>
    public double? ConsumeRatePerSecond { get; set; }

    /// <summary>
    /// Estimated write rate for this topic.
    /// </summary>
    public double? WriteRatePerSecond { get; set; }

    /// <summary>
    /// Member-level metrics and partition assignments.
    /// </summary>
    public IReadOnlyList<KafkaConsumerMemberMetrics> Members { get; set; } = [];
}

/// <summary>
/// Captured metrics for one consumer-group member.
/// </summary>
public sealed class KafkaConsumerMemberMetrics
{
    /// <summary>
    /// Broker-generated consumer identifier. An empty value denotes an unassigned partition bucket.
    /// </summary>
    public string ConsumerId { get; set; } = string.Empty;

    /// <summary>
    /// Member host.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Member client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Partition-level metrics assigned to this member.
    /// </summary>
    public IReadOnlyList<KafkaConsumerPartitionMetrics> Partitions { get; set; } = [];
}

/// <summary>
/// Offset and rate metrics for one partition from a consumer-group perspective.
/// </summary>
public sealed class KafkaConsumerPartitionMetrics
{
    /// <summary>
    /// Partition number.
    /// </summary>
    public int Partition { get; set; }

    /// <summary>
    /// Current committed offset, or <see langword="null"/> when the group has no committed offset.
    /// </summary>
    public long? CurrentOffset { get; set; }

    /// <summary>
    /// Log-end offset returned by Kafka.
    /// </summary>
    public long? LogEndOffset { get; set; }

    /// <summary>
    /// Lag calculated as log-end offset minus current offset.
    /// </summary>
    public long? Lag { get; set; }

    /// <summary>
    /// Estimated consumer rate for this partition.
    /// </summary>
    public double? ConsumeRatePerSecond { get; set; }

    /// <summary>
    /// Estimated write rate for this partition.
    /// </summary>
    public double? WriteRatePerSecond { get; set; }
}

/// <summary>
/// Result of a detailed consumer metrics capture.
/// </summary>
public sealed class KafkaConsumerMetricsSnapshot
{
    /// <summary>
    /// Target cluster identifier.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// UTC capture time.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional diagnostic message returned by the sampler.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Live consumer-group descriptions used for this capture.
    /// </summary>
    public IReadOnlyList<KafkaConsumerGroupDescription> ConsumerGroups { get; set; } = [];

    /// <summary>
    /// Topic/group metrics captured during this cycle.
    /// </summary>
    public IReadOnlyList<KafkaConsumerGroupTopicMetrics> GroupMetrics { get; set; } = [];

    /// <summary>
    /// Aggregate offset totals derived from the same broker queries, when supplied by the provider.
    /// </summary>
    public KafkaPerformanceOffsetTotals? PerformanceOffsetTotals { get; set; }
}
