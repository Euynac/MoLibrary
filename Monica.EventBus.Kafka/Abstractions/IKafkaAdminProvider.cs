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

    /// <summary>
    /// Describes one consumer group, including its live member identities and partition assignments.
    /// </summary>
    /// <param name="cluster">Target Kafka cluster.</param>
    /// <param name="groupId">Consumer group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral group description.</returns>
    Task<KafkaConsumerGroupDescription> DescribeConsumerGroupAsync(
        KafkaClusterConfig cluster,
        string groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes multiple consumer groups in one provider operation when supported.
    /// </summary>
    /// <remarks>
    /// The default implementation composes the single-group contract. Providers backed by a batch
    /// admin API should override this method to avoid one broker round trip per group.
    /// </remarks>
    /// <param name="cluster">Target Kafka cluster.</param>
    /// <param name="groupIds">Consumer group identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptions in the requested order. Failed rows carry an error message.</returns>
    async Task<IReadOnlyList<KafkaConsumerGroupDescription>> DescribeConsumerGroupsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        var descriptions = new List<KafkaConsumerGroupDescription>(groupIds.Count);
        foreach (var groupId in groupIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                descriptions.Add(await DescribeConsumerGroupAsync(cluster, groupId, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                descriptions.Add(new KafkaConsumerGroupDescription
                {
                    GroupId = groupId,
                    ErrorMessage = ex.Message
                });
            }
        }

        return descriptions;
    }
}
