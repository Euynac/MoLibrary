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
    private readonly ConcurrentDictionary<string, List<KafkaPerformanceSnapshot>> _snapshots = new(StringComparer.Ordinal);

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
        _snapshots.TryRemove(clusterId, out _);
        return Task.CompletedTask;
    }

    public Task SavePerformanceSnapshotAsync(KafkaPerformanceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var list = _snapshots.GetOrAdd(snapshot.ClusterId, _ => []);
        lock (list)
        {
            list.Add(CloneSnapshot(snapshot));
            if (list.Count > 500)
            {
                list.RemoveRange(0, list.Count - 500);
            }
        }

        return Task.CompletedTask;
    }

    public Task<KafkaPerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(clusterId, out var list))
        {
            return Task.FromResult<KafkaPerformanceSnapshot?>(null);
        }

        lock (list)
        {
            return Task.FromResult(list.LastOrDefault() is { } snapshot
                ? CloneSnapshot(snapshot)
                : null);
        }
    }

    public Task<IReadOnlyList<KafkaPerformanceSnapshot>> GetPerformanceSnapshotsAsync(
        string clusterId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(clusterId, out var list))
        {
            return Task.FromResult<IReadOnlyList<KafkaPerformanceSnapshot>>([]);
        }

        lock (list)
        {
            var snapshots = list
                .OrderByDescending(snapshot => snapshot.CapturedAt)
                .Take(Math.Max(1, limit))
                .OrderBy(snapshot => snapshot.CapturedAt)
                .Select(CloneSnapshot)
                .ToList();
            return Task.FromResult<IReadOnlyList<KafkaPerformanceSnapshot>>(snapshots);
        }
    }

    public Task DeletePerformanceSnapshotsOlderThanAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default)
    {
        foreach (var (_, list) in _snapshots)
        {
            lock (list)
            {
                list.RemoveAll(snapshot => snapshot.CapturedAt < olderThanUtc);
            }
        }

        return Task.CompletedTask;
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
            TotalConsumerCommittedOffset = snapshot.TotalConsumerCommittedOffset,
            MessageWriteRatePerSecond = snapshot.MessageWriteRatePerSecond,
            MessageConsumeRatePerSecond = snapshot.MessageConsumeRatePerSecond,
            IncludesJmxMetrics = snapshot.IncludesJmxMetrics,
            Message = snapshot.Message
        };
    }
}
