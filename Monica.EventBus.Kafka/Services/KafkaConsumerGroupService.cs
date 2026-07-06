using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates Kafka consumer group inspection use cases.
/// </summary>
public sealed class KafkaConsumerGroupService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider)
{
    public async Task<IReadOnlyList<KafkaConsumerGroupSummary>> ListConsumerGroupsAsync(
        string clusterId,
        CancellationToken cancellationToken = default)
    {
        var cluster = await clusterService.GetRequiredClusterAsync(clusterId, cancellationToken);
        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' is visible but direct consumer group access is not configured.");
        }

        return await adminProvider.ListConsumerGroupsAsync(cluster, cancellationToken);
    }
}
