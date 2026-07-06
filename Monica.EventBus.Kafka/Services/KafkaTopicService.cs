using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates Kafka topic administration use cases.
/// </summary>
public sealed class KafkaTopicService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider)
{
    public async Task<IReadOnlyList<KafkaTopicSummary>> ListTopicsAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = await GetAdminClusterAsync(clusterId, cancellationToken);
        return await adminProvider.ListTopicsAsync(cluster, cancellationToken);
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

    private async Task<KafkaClusterConfig> GetAdminClusterAsync(string clusterId, CancellationToken cancellationToken)
    {
        var cluster = await clusterService.GetRequiredClusterAsync(clusterId, cancellationToken);
        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' is visible but direct admin access is not configured.");
        }

        return cluster;
    }
}
