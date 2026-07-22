using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Abstractions;

/// <summary>
/// Executes direct Kafka broker operations used by the Kafka console.
/// </summary>
/// <remarks>
/// Implementations should throw provider-specific exceptions for failed broker operations. Facades
/// are responsible for converting those exceptions to Monica result envelopes.
/// </remarks>
public interface IKafkaAdminProvider
{
    /// <summary>
    /// Tests whether the target cluster can be reached.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection metadata collected during the probe.</returns>
    Task<KafkaConnectionTestResult> TestConnectionAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists brokers known by the target cluster metadata.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Broker metadata.</returns>
    Task<IReadOnlyList<KafkaBrokerInfo>> ListBrokersAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists topics in the target cluster.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topic summaries.</returns>
    Task<IReadOnlyList<KafkaTopicSummary>> ListTopicsAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists topic metadata without requesting per-topic configuration values.
    /// </summary>
    /// <remarks>
    /// Performance sampling uses this lightweight path so a large topic inventory does not pay
    /// for a <c>DescribeConfigs</c> request on every sample.
    /// </remarks>
    /// <param name="cluster">Target Kafka cluster.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All topic names returned by Kafka metadata, including rows with transient metadata errors.</returns>
    Task<IReadOnlyList<KafkaTopicSummary>> ListTopicMetadataAsync(
        KafkaClusterConfig cluster,
        CancellationToken cancellationToken = default)
    {
        // Provider implementations can override this with a metadata-only query. The default
        // keeps custom providers source-compatible while retaining the existing topic contract.
        return ListTopicsAsync(cluster, cancellationToken);
    }

    /// <summary>
    /// Creates a topic.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="request">Topic creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateTopicAsync(KafkaClusterConfig cluster, KafkaTopicCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a topic.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="topicName">Topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteTopicAsync(KafkaClusterConfig cluster, string topicName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all currently retained records from every partition of a topic while keeping the
    /// topic, its partitions, and its configuration intact.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="topicName">Topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Implementations should use Kafka's record-deletion API rather than deleting and recreating
    /// the topic. Consumer-group offsets are not reset by this operation.
    /// </remarks>
    Task ClearTopicMessagesAsync(KafkaClusterConfig cluster, string topicName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increases a topic's partition count.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="request">Partition update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IncreasePartitionsAsync(KafkaClusterConfig cluster, KafkaTopicPartitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a topic's retention time.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="request">Retention update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateRetentionAsync(KafkaClusterConfig cluster, KafkaTopicRetentionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists consumer groups in the target cluster.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consumer group summaries.</returns>
    Task<IReadOnlyList<KafkaConsumerGroupSummary>> ListConsumerGroupsAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default);
}
