using System.Collections.Concurrent;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// In-memory Kafka console repository used when no persistent store is configured.
/// </summary>
internal sealed class InMemoryKafkaConsoleRepository : IKafkaConsoleRepository
{
    private readonly ConcurrentDictionary<string, KafkaClusterConfig> _clusters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, KafkaPerformanceSnapshot> _latestSnapshots = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<KafkaClusterConfig>> GetClustersAsync(CancellationToken cancellationToken = default)
    {
        var clusters = _clusters.Values
            .Select(cluster => cluster.Clone())
            .OrderBy(cluster => cluster.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<KafkaClusterConfig>>(clusters);
    }

    public Task<KafkaClusterConfig?> GetClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _clusters.TryGetValue(clusterId, out var cluster)
                ? cluster.Clone()
                : null);
    }

    public Task UpsertClusterAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        var normalized = cluster.Clone().Normalize();
        normalized.UpdatedAt = DateTimeOffset.UtcNow;
        normalized.CreatedAt = _clusters.TryGetValue(normalized.ClusterId, out var existing)
            ? existing.CreatedAt
            : normalized.CreatedAt;
        _clusters[normalized.ClusterId] = normalized;
        return Task.CompletedTask;
    }

    public Task DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        _clusters.TryRemove(clusterId, out _);
        _latestSnapshots.TryRemove(clusterId, out _);
        return Task.CompletedTask;
    }

    public Task ReplacePerformanceSnapshotAsync(KafkaPerformanceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _latestSnapshots[snapshot.ClusterId] = CloneSnapshot(snapshot);
        return Task.CompletedTask;
    }

    public Task<KafkaPerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _latestSnapshots.TryGetValue(clusterId, out var snapshot)
                ? CloneSnapshot(snapshot)
                : null);
    }

    private static KafkaPerformanceSnapshot CloneSnapshot(KafkaPerformanceSnapshot snapshot)
    {
        return new KafkaPerformanceSnapshot
        {
            ClusterId = snapshot.ClusterId,
            CapturedAt = snapshot.CapturedAt,
            BrokerCount = snapshot.BrokerCount,
            TopicCount = snapshot.TopicCount,
            ConsumerGroupCount = snapshot.ConsumerGroupCount,
            TotalLag = snapshot.TotalLag,
            TotalLogEndOffset = snapshot.TotalLogEndOffset,
            TotalAvailableMessageCount = snapshot.TotalAvailableMessageCount,
            TotalConsumerCommittedOffset = snapshot.TotalConsumerCommittedOffset,
            MessageWriteRatePerSecond = snapshot.MessageWriteRatePerSecond,
            MessageConsumeRatePerSecond = snapshot.MessageConsumeRatePerSecond,
            IncludesJmxMetrics = snapshot.IncludesJmxMetrics,
            Message = snapshot.Message,
            TopicMetrics = snapshot.TopicMetrics
                .Select(metric => new KafkaTopicPerformanceSnapshot
                {
                    TopicName = metric.TopicName,
                    TotalLogEndOffset = metric.TotalLogEndOffset,
                    TotalAvailableMessageCount = metric.TotalAvailableMessageCount,
                    TotalConsumerCommittedOffset = metric.TotalConsumerCommittedOffset,
                    TotalLag = metric.TotalLag,
                    MessageWriteRatePerSecond = metric.MessageWriteRatePerSecond,
                    MessageConsumeRatePerSecond = metric.MessageConsumeRatePerSecond
                })
                .ToList()
        };
    }
}
