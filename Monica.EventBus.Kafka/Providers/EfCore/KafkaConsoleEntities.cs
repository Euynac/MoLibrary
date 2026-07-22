using System.Text.Json;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Providers.EfCore;

/// <summary>
/// Persisted Kafka cluster entity.
/// </summary>
public sealed class KafkaClusterEntity
{
    /// <summary>
    /// Cluster identifier.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Bootstrap server list.
    /// </summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Optional client id.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Optional security protocol.
    /// </summary>
    public string? SecurityProtocol { get; set; }

    /// <summary>
    /// Optional SASL mechanism.
    /// </summary>
    public string? SaslMechanism { get; set; }

    /// <summary>
    /// Optional SASL user name.
    /// </summary>
    public string? SaslUsername { get; set; }

    /// <summary>
    /// Optional SASL password.
    /// </summary>
    public string? SaslPassword { get; set; }

    /// <summary>
    /// Optional SSL CA location.
    /// </summary>
    public string? SslCaLocation { get; set; }

    /// <summary>
    /// Optional JMX endpoint.
    /// </summary>
    public string? JmxEndpoint { get; set; }

    /// <summary>
    /// Optional Dapr pub/sub name.
    /// </summary>
    public string? DaprPubSubName { get; set; }

    /// <summary>
    /// Whether the cluster backs Dapr pub/sub.
    /// </summary>
    public bool IsDaprBacked { get; set; }

    /// <summary>
    /// Whether credentials are stored outside Monica.
    /// </summary>
    public bool CredentialsManagedExternally { get; set; }

    /// <summary>
    /// UTC creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// UTC update time.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Converts the entity to a public model.
    /// </summary>
    public KafkaClusterConfig ToModel()
    {
        return new KafkaClusterConfig
        {
            ClusterId = ClusterId,
            DisplayName = DisplayName,
            BootstrapServers = BootstrapServers,
            ClientId = ClientId,
            SecurityProtocol = SecurityProtocol,
            SaslMechanism = SaslMechanism,
            SaslUsername = SaslUsername,
            SaslPassword = SaslPassword,
            SslCaLocation = SslCaLocation,
            JmxEndpoint = JmxEndpoint,
            DaprPubSubName = DaprPubSubName,
            IsDaprBacked = IsDaprBacked,
            CredentialsManagedExternally = CredentialsManagedExternally,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    /// <summary>
    /// Converts a public model to an entity.
    /// </summary>
    public static KafkaClusterEntity FromModel(KafkaClusterConfig model)
    {
        var normalized = model.Clone().Normalize();
        return new KafkaClusterEntity
        {
            ClusterId = normalized.ClusterId,
            DisplayName = normalized.DisplayName,
            BootstrapServers = normalized.BootstrapServers,
            ClientId = normalized.ClientId,
            SecurityProtocol = normalized.SecurityProtocol,
            SaslMechanism = normalized.SaslMechanism,
            SaslUsername = normalized.SaslUsername,
            SaslPassword = normalized.SaslPassword,
            SslCaLocation = normalized.SslCaLocation,
            JmxEndpoint = normalized.JmxEndpoint,
            DaprPubSubName = normalized.DaprPubSubName,
            IsDaprBacked = normalized.IsDaprBacked,
            CredentialsManagedExternally = normalized.CredentialsManagedExternally,
            CreatedAt = normalized.CreatedAt,
            UpdatedAt = normalized.UpdatedAt
        };
    }
}

/// <summary>
/// Persisted Kafka performance snapshot entity.
/// </summary>
public sealed class KafkaPerformanceSnapshotEntity
{
    private static readonly JsonSerializerOptions TOPIC_METRICS_JSON_OPTIONS = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Entity id.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Cluster identifier.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// UTC capture time.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; }

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
    /// Total lag when available.
    /// </summary>
    public long? TotalLag { get; set; }

    /// <summary>
    /// Sum of latest offsets across sampled non-internal topic partitions.
    /// </summary>
    public long? TotalLogEndOffset { get; set; }

    /// <summary>
    /// Total retained messages across all sampled non-internal topic partitions.
    /// </summary>
    public long? TotalAvailableMessageCount { get; set; }

    /// <summary>
    /// Sum of committed offsets across sampled consumer groups and non-internal topic partitions.
    /// </summary>
    public long? TotalConsumerCommittedOffset { get; set; }

    /// <summary>
    /// Estimated topic write rate in messages per second.
    /// </summary>
    public double? MessageWriteRatePerSecond { get; set; }

    /// <summary>
    /// Estimated consumer processing rate in messages per second.
    /// </summary>
    public double? MessageConsumeRatePerSecond { get; set; }

    /// <summary>
    /// Whether JMX metrics were included.
    /// </summary>
    public bool IncludesJmxMetrics { get; set; }

    /// <summary>
    /// Optional sampling message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Serialized per-topic metrics captured with this snapshot.
    /// </summary>
    /// <remarks>
    /// Keeping the collection in one provider-neutral column avoids coupling the console model to a
    /// particular relational provider's JSON or owned-collection support.
    /// </remarks>
    public string? TopicMetricsJson { get; set; }

    /// <summary>
    /// Converts the entity to a public model.
    /// </summary>
    public KafkaPerformanceSnapshot ToModel()
    {
        return new KafkaPerformanceSnapshot
        {
            ClusterId = ClusterId,
            CapturedAt = CapturedAt,
            BrokerCount = BrokerCount,
            TopicCount = TopicCount,
            ConsumerGroupCount = ConsumerGroupCount,
            TotalLag = TotalLag,
            TotalLogEndOffset = TotalLogEndOffset,
            TotalAvailableMessageCount = TotalAvailableMessageCount,
            TotalConsumerCommittedOffset = TotalConsumerCommittedOffset,
            MessageWriteRatePerSecond = MessageWriteRatePerSecond,
            MessageConsumeRatePerSecond = MessageConsumeRatePerSecond,
            IncludesJmxMetrics = IncludesJmxMetrics,
            Message = Message,
            TopicMetrics = DeserializeTopicMetrics(TopicMetricsJson)
        };
    }

    /// <summary>
    /// Converts a public model to an entity.
    /// </summary>
    public static KafkaPerformanceSnapshotEntity FromModel(KafkaPerformanceSnapshot model)
    {
        return new KafkaPerformanceSnapshotEntity
        {
            ClusterId = model.ClusterId,
            CapturedAt = model.CapturedAt,
            BrokerCount = model.BrokerCount,
            TopicCount = model.TopicCount,
            ConsumerGroupCount = model.ConsumerGroupCount,
            TotalLag = model.TotalLag,
            TotalLogEndOffset = model.TotalLogEndOffset,
            TotalAvailableMessageCount = model.TotalAvailableMessageCount,
            TotalConsumerCommittedOffset = model.TotalConsumerCommittedOffset,
            MessageWriteRatePerSecond = model.MessageWriteRatePerSecond,
            MessageConsumeRatePerSecond = model.MessageConsumeRatePerSecond,
            IncludesJmxMetrics = model.IncludesJmxMetrics,
            Message = model.Message,
            TopicMetricsJson = SerializeTopicMetrics(model.TopicMetrics)
        };
    }

    private static IReadOnlyList<KafkaTopicPerformanceSnapshot> DeserializeTopicMetrics(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<KafkaTopicPerformanceSnapshot>>(
                       json,
                       TOPIC_METRICS_JSON_OPTIONS)
                   ?? [];
        }
        catch (JsonException)
        {
            // A malformed optional payload should not make the entire historical snapshot unreadable.
            return [];
        }
    }

    private static string? SerializeTopicMetrics(IReadOnlyList<KafkaTopicPerformanceSnapshot> metrics)
    {
        return metrics.Count == 0
            ? null
            : JsonSerializer.Serialize(metrics, TOPIC_METRICS_JSON_OPTIONS);
    }
}
