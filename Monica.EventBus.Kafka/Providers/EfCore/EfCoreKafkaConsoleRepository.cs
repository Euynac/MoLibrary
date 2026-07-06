using Microsoft.EntityFrameworkCore;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Providers.EfCore;

/// <summary>
/// EF Core-backed Kafka console repository.
/// </summary>
public sealed class EfCoreKafkaConsoleRepository(KafkaConsoleDbContext dbContext) : IKafkaConsoleRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<KafkaClusterConfig>> GetClustersAsync(CancellationToken cancellationToken = default)
    {
        var clusters = await dbContext.KafkaClusters
            .AsNoTracking()
            .OrderBy(cluster => cluster.DisplayName)
            .ToListAsync(cancellationToken);

        return clusters.Select(cluster => cluster.ToModel()).ToList();
    }

    /// <inheritdoc />
    public async Task<KafkaClusterConfig?> GetClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KafkaClusters
            .AsNoTracking()
            .FirstOrDefaultAsync(cluster => cluster.ClusterId == clusterId, cancellationToken);
        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task UpsertClusterAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        var normalized = cluster.Clone().Normalize();
        var existing = await dbContext.KafkaClusters
            .FirstOrDefaultAsync(item => item.ClusterId == normalized.ClusterId, cancellationToken);

        if (existing is null)
        {
            normalized.CreatedAt = normalized.CreatedAt == default ? DateTimeOffset.UtcNow : normalized.CreatedAt;
            normalized.UpdatedAt = DateTimeOffset.UtcNow;
            dbContext.KafkaClusters.Add(KafkaClusterEntity.FromModel(normalized));
        }
        else
        {
            var updated = KafkaClusterEntity.FromModel(normalized);
            updated.CreatedAt = existing.CreatedAt;
            updated.UpdatedAt = DateTimeOffset.UtcNow;
            dbContext.Entry(existing).CurrentValues.SetValues(updated);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        await dbContext.KafkaClusters
            .Where(cluster => cluster.ClusterId == clusterId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.KafkaPerformanceSnapshots
            .Where(snapshot => snapshot.ClusterId == clusterId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SavePerformanceSnapshotAsync(KafkaPerformanceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        dbContext.KafkaPerformanceSnapshots.Add(KafkaPerformanceSnapshotEntity.FromModel(snapshot));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<KafkaPerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KafkaPerformanceSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.ClusterId == clusterId)
            .OrderByDescending(snapshot => snapshot.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KafkaPerformanceSnapshot>> GetPerformanceSnapshotsAsync(
        string clusterId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await dbContext.KafkaPerformanceSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.ClusterId == clusterId)
            .OrderByDescending(snapshot => snapshot.CapturedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        return snapshots
            .Select(snapshot => snapshot.ToModel())
            .Reverse()
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeletePerformanceSnapshotsOlderThanAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default)
    {
        await dbContext.KafkaPerformanceSnapshots
            .Where(snapshot => snapshot.CapturedAt < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
