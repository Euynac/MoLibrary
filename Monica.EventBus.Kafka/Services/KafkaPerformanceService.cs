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
    KafkaConsumerMetricsService consumerMetricsService,
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

            var consumerMetrics = await consumerMetricsService.CaptureForPerformanceAsync(
                cluster,
                topics,
                groups,
                cancellationToken);
            var offsetTotals = consumerMetrics.PerformanceOffsetTotals ??
                               await offsetMetricsProvider.CapturePerformanceOffsetTotalsAsync(
                                   cluster,
                                   topics,
                                   groups,
                                   cancellationToken);
            snapshot.TotalLag = offsetTotals.AreConsumerOffsetsComplete ? offsetTotals.TotalLag : null;
            snapshot.TotalLogEndOffset = offsetTotals.AreTopicOffsetsComplete ? offsetTotals.TotalLogEndOffset : null;
            snapshot.TotalRetainedMessageCount = offsetTotals.AreTopicOffsetsComplete
                ? offsetTotals.TotalRetainedMessageCount
                : null;
            snapshot.TotalConsumerCommittedOffset = offsetTotals.AreConsumerOffsetsComplete
                ? offsetTotals.TotalConsumerCommittedOffset
                : null;
            snapshot.TopicMetrics = offsetTotals.TopicTotals
                .Select(total => new KafkaTopicPerformanceSnapshot
                {
                    TopicName = total.TopicName,
                    TotalLogEndOffset = total.TotalLogEndOffset,
                    TotalRetainedMessageCount = total.TotalRetainedMessageCount,
                    TotalConsumerCommittedOffset = total.TotalConsumerCommittedOffset,
                    TotalLag = total.TotalLag
                })
                .ToList();
            snapshot.ConsumerGroupMetrics = consumerMetrics.GroupMetrics;
            if (!string.IsNullOrWhiteSpace(consumerMetrics.Message))
            {
                snapshot.Message = consumerMetrics.Message;
            }

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
