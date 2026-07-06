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
    Task TestConnectionAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default);

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
