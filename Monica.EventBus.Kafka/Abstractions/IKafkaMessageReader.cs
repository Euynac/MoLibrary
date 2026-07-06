using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Abstractions;

/// <summary>
/// Reads bounded Kafka message previews for console inspection.
/// </summary>
/// <remarks>
/// Implementations must not commit offsets or otherwise mutate application consumer state.
/// Message previews are diagnostic data and should be bounded by module limits to avoid loading
/// large payloads into the UI.
/// </remarks>
public interface IKafkaMessageReader
{
    /// <summary>
    /// Reads a bounded sample of recent messages from a topic.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="request">Message preview request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topic message sample.</returns>
    Task<KafkaTopicMessageBatch> ReadMessagesAsync(
        KafkaClusterConfig cluster,
        KafkaTopicMessagesRequest request,
        CancellationToken cancellationToken = default);
}
