using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates Kafka topic administration use cases.
/// </summary>
public sealed class KafkaTopicService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider,
    IKafkaOffsetMetricsProvider offsetMetricsProvider,
    IKafkaMessageReader messageReader)
{
    public async Task<IReadOnlyList<KafkaTopicSummary>> ListTopicsAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(clusterId, cancellationToken);
        var topics = (await adminProvider.ListTopicsAsync(cluster, cancellationToken)).ToList();
        IReadOnlyList<KafkaTopicBacklogSnapshot> backlogs;
        try
        {
            // Topic metadata is the authoritative inventory. Offset ranges are optional
            // enrichment and may be unavailable for a large or partially healthy cluster.
            backlogs = await offsetMetricsProvider.CaptureTopicBacklogsAsync(cluster, topics, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            backlogs = [];
        }
        catch
        {
            backlogs = [];
        }
        var backlogsByTopic = backlogs.ToDictionary(
            backlog => backlog.TopicName,
            StringComparer.Ordinal);

        foreach (var topic in topics)
        {
            if (backlogsByTopic.TryGetValue(topic.TopicName, out var backlog))
            {
                topic.RetainedMessageCount = backlog.TotalRetainedMessageCount;
            }
        }

        return topics;
    }

    public async Task CreateTopicAsync(KafkaTopicCreateRequest request, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(request.ClusterId, cancellationToken);
        await adminProvider.CreateTopicAsync(cluster, request, cancellationToken);
    }

    public async Task DeleteTopicAsync(string clusterId, string topicName, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(clusterId, cancellationToken);
        await adminProvider.DeleteTopicAsync(cluster, topicName, cancellationToken);
    }

    /// <summary>
    /// Clears all retained records from every partition of a topic without deleting the topic.
    /// </summary>
    /// <param name="clusterId">Target cluster id.</param>
    /// <param name="topicName">Topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClearTopicMessagesAsync(
        string clusterId,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(clusterId, cancellationToken);
        await adminProvider.ClearTopicMessagesAsync(cluster, topicName, cancellationToken);
    }

    public async Task IncreasePartitionsAsync(KafkaTopicPartitionRequest request, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(request.ClusterId, cancellationToken);
        await adminProvider.IncreasePartitionsAsync(cluster, request, cancellationToken);
    }

    public async Task UpdateRetentionAsync(KafkaTopicRetentionRequest request, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(request.ClusterId, cancellationToken);
        await adminProvider.UpdateRetentionAsync(cluster, request, cancellationToken);
    }

    public async Task<KafkaTopicMessageBatch> ReadMessagesAsync(KafkaTopicMessagesRequest request, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(request.ClusterId, cancellationToken);
        return await messageReader.ReadMessagesAsync(cluster, request, cancellationToken);
    }

    public async Task<KafkaTopicBacklogSnapshot> GetTopicBacklogAsync(
        string clusterId,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(clusterId, cancellationToken);
        return await offsetMetricsProvider.CaptureTopicBacklogAsync(cluster, topicName, cancellationToken);
    }

    private async Task<KafkaClusterConfig> GetAdminClusterAsync(string clusterId, CancellationToken cancellationToken)
    {
        return await clusterService.GetRequiredDirectAdminClusterAsync(clusterId, cancellationToken);
    }
}
