using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Captures and reads Kafka performance snapshots used by the console.
/// </summary>
public sealed class KafkaPerformanceService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider,
    IKafkaOffsetMetricsProvider offsetMetricsProvider,
    IKafkaConsoleRepository repository)
{
    public async Task<KafkaPerformanceSnapshot?> GetLatestAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        return await repository.GetLatestPerformanceSnapshotAsync(clusterId.Trim(), cancellationToken);
    }

    public async Task<KafkaPerformanceSnapshot> CaptureAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = await clusterService.GetRequiredClusterAsync(clusterId, cancellationToken);
        var previous = await repository.GetLatestPerformanceSnapshotAsync(cluster.ClusterId, cancellationToken);
        var snapshot = new KafkaPerformanceSnapshot
        {
            ClusterId = cluster.ClusterId,
            CapturedAt = DateTimeOffset.UtcNow,
            IncludesJmxMetrics = !string.IsNullOrWhiteSpace(cluster.JmxEndpoint)
        };

        if (!cluster.HasDirectKafkaAccess)
        {
            snapshot.Message = "Direct Kafka access is not configured; performance sampling is limited to configuration visibility.";
            await repository.ReplacePerformanceSnapshotAsync(snapshot, cancellationToken);
            return snapshot;
        }

        try
        {
            await clusterService.EnsureDirectKafkaAccessAsync(cluster, cancellationToken);

            var brokers = await adminProvider.ListBrokersAsync(cluster, cancellationToken);
            // Performance sampling only needs partition metadata. Avoid issuing a full topic
            // configuration scan on every live sample; the topic-management path enriches rows
            // with retention independently.
            var topics = await adminProvider.ListTopicMetadataAsync(cluster, cancellationToken);
            var groups = await adminProvider.ListConsumerGroupsAsync(cluster, cancellationToken);

            snapshot.BrokerCount = brokers.Count;
            snapshot.TopicCount = topics.Count;
            snapshot.ConsumerGroupCount = groups.Count;

            var offsetTotals = await offsetMetricsProvider.CapturePerformanceOffsetTotalsAsync(cluster, topics, groups, cancellationToken);
            snapshot.TotalLag = offsetTotals.IsComplete ? offsetTotals.TotalLag : null;
            snapshot.TotalLogEndOffset = offsetTotals.IsComplete ? offsetTotals.TotalLogEndOffset : null;
            snapshot.TotalAvailableMessageCount = offsetTotals.IsComplete
                ? offsetTotals.TotalAvailableMessageCount
                : null;
            snapshot.TotalConsumerCommittedOffset = offsetTotals.IsComplete
                ? offsetTotals.TotalConsumerCommittedOffset
                : null;
            snapshot.TopicMetrics = offsetTotals.TopicTotals
                .Select(total => new KafkaTopicPerformanceSnapshot
                {
                    TopicName = total.TopicName,
                    TotalLogEndOffset = total.TotalLogEndOffset,
                    TotalAvailableMessageCount = total.TotalAvailableMessageCount,
                    TotalConsumerCommittedOffset = total.TotalConsumerCommittedOffset,
                    TotalLag = total.TotalLag
                })
                .ToList();
            KafkaPerformanceRateCalculator.ApplyRates(snapshot, previous);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            snapshot.Message = ex.Message;
        }

        await repository.ReplacePerformanceSnapshotAsync(snapshot, cancellationToken);
        return snapshot;
    }
}
