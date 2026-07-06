using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Abstractions;

/// <summary>
/// Captures Kafka offset totals used by the console to estimate message rates.
/// </summary>
/// <remarks>
/// Implementations should use read-only Kafka admin APIs and must not commit offsets or alter
/// consumer group state.
/// </remarks>
public interface IKafkaPerformanceMetricsProvider
{
    /// <summary>
    /// Captures current Kafka offset totals for visible non-internal topics and consumer groups.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="topics">Topic summaries from the same sampling cycle.</param>
    /// <param name="consumerGroups">Consumer group summaries from the same sampling cycle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Offset totals for rate calculation.</returns>
    Task<KafkaPerformanceOffsetTotals> CaptureOffsetTotalsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken = default);
}
