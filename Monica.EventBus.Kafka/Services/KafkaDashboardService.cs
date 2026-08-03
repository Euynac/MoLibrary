using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Builds dashboard aggregates for the selected Kafka cluster.
/// </summary>
public sealed class KafkaDashboardService(
    KafkaIntegrationService integrationService,
    KafkaClusterService clusterService,
    KafkaPerformanceService performanceService)
{
    public async Task<KafkaDashboardSnapshot> GetDashboardAsync(string? clusterId = null, CancellationToken cancellationToken = default)
    {
        var integration = await integrationService.GetSnapshotAsync(cancellationToken);
        var clusters = await clusterService.ListClustersAsync(cancellationToken);
        var selectedCluster = ResolveSelectedCluster(clusters, clusterId, integration.PrimaryClusterId);
        var latestPerformance = selectedCluster is null
            ? null
            : await performanceService.GetLatestAsync(selectedCluster.Config.ClusterId, cancellationToken);

        return new KafkaDashboardSnapshot
        {
            Integration = integration,
            SelectedCluster = selectedCluster,
            LatestPerformance = latestPerformance,
            BrokerCount = ResolveCount(selectedCluster?.BrokerCount, latestPerformance?.BrokerCount),
            TopicCount = ResolveCount(selectedCluster?.TopicCount, latestPerformance?.TopicCount),
            ConsumerGroupCount = ResolveCount(selectedCluster?.ConsumerGroupCount, latestPerformance?.ConsumerGroupCount),
            TotalRetainedMessageCount = null
        };
    }

    private static int ResolveCount(int? liveCount, int? sampledCount)
    {
        return liveCount is > 0 ? liveCount.Value : sampledCount.GetValueOrDefault();
    }

    private static KafkaClusterSummary? ResolveSelectedCluster(
        IReadOnlyList<KafkaClusterSummary> clusters,
        string? requestedClusterId,
        string? primaryClusterId)
    {
        if (!string.IsNullOrWhiteSpace(requestedClusterId))
        {
            var requested = clusters.FirstOrDefault(cluster =>
                string.Equals(cluster.Config.ClusterId, requestedClusterId, StringComparison.Ordinal));
            if (requested is not null)
            {
                return requested;
            }
        }

        if (!string.IsNullOrWhiteSpace(primaryClusterId))
        {
            var primary = clusters.FirstOrDefault(cluster =>
                string.Equals(cluster.Config.ClusterId, primaryClusterId, StringComparison.Ordinal));
            if (primary is not null)
            {
                return primary;
            }
        }

        return clusters.FirstOrDefault();
    }
}
