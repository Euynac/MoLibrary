using Microsoft.Extensions.Options;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Captures and reads Kafka performance snapshots used by the console.
/// </summary>
public sealed class KafkaPerformanceService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider,
    IKafkaConsoleRepository repository,
    IOptions<ModuleEventBusKafkaOption> options)
{
    public async Task<KafkaPerformanceSnapshot?> GetLatestAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        return await repository.GetLatestPerformanceSnapshotAsync(clusterId.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<KafkaPerformanceSnapshot>> GetHistoryAsync(
        string clusterId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        return await repository.GetPerformanceSnapshotsAsync(
            clusterId.Trim(),
            limit.GetValueOrDefault(options.Value.PerformanceHistoryLimit),
            cancellationToken);
    }

    public async Task<KafkaPerformanceSnapshot> CaptureAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = await clusterService.GetRequiredClusterAsync(clusterId, cancellationToken);
        var snapshot = new KafkaPerformanceSnapshot
        {
            ClusterId = cluster.ClusterId,
            CapturedAt = DateTimeOffset.UtcNow,
            IncludesJmxMetrics = !string.IsNullOrWhiteSpace(cluster.JmxEndpoint)
        };

        if (!cluster.HasDirectKafkaAccess)
        {
            snapshot.Message = "Direct Kafka access is not configured; performance sampling is limited to configuration visibility.";
            await SaveSnapshotAsync(snapshot, cancellationToken);
            return snapshot;
        }

        try
        {
            var brokers = await adminProvider.ListBrokersAsync(cluster, cancellationToken);
            var topics = await adminProvider.ListTopicsAsync(cluster, cancellationToken);
            var groups = await adminProvider.ListConsumerGroupsAsync(cluster, cancellationToken);

            snapshot.BrokerCount = brokers.Count;
            snapshot.TopicCount = topics.Count;
            snapshot.ConsumerGroupCount = groups.Count;
            snapshot.TotalLag = groups.Any(group => group.TotalLag.HasValue)
                ? groups.Sum(group => group.TotalLag.GetValueOrDefault())
                : null;
        }
        catch (Exception ex)
        {
            snapshot.Message = ex.Message;
        }

        await SaveSnapshotAsync(snapshot, cancellationToken);
        return snapshot;
    }

    private async Task SaveSnapshotAsync(KafkaPerformanceSnapshot snapshot, CancellationToken cancellationToken)
    {
        await repository.SavePerformanceSnapshotAsync(snapshot, cancellationToken);
        if (options.Value.PerformanceSnapshotRetention > TimeSpan.Zero)
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(options.Value.PerformanceSnapshotRetention);
            await repository.DeletePerformanceSnapshotsOlderThanAsync(cutoff, cancellationToken);
        }
    }
}
