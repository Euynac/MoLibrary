using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Abstractions;

/// <summary>
/// Captures Kafka offset metrics used by the console.
/// </summary>
/// <remarks>
/// Implementations should use read-only Kafka admin APIs and must not commit offsets or alter
/// consumer group state.
/// </remarks>
public interface IKafkaOffsetMetricsProvider
{
    /// <summary>
    /// Captures current retained-message counts for one topic.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="topicName">Topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topic backlog snapshot with partition-level offset ranges.</returns>
    Task<KafkaTopicBacklogSnapshot> CaptureTopicBacklogAsync(
        KafkaClusterConfig cluster,
        string topicName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures current retained-message counts for visible topics.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="topics">Topic summaries from the same metadata query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topic backlog snapshots keyed by topic name.</returns>
    Task<IReadOnlyList<KafkaTopicBacklogSnapshot>> CaptureTopicBacklogsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures current Kafka offset totals for visible non-internal topics and consumer groups.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="topics">Topic summaries from the same sampling cycle.</param>
    /// <param name="consumerGroups">Consumer group summaries from the same sampling cycle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Offset totals for rate calculation and inventory display.</returns>
    Task<KafkaPerformanceOffsetTotals> CapturePerformanceOffsetTotalsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken = default);
}
