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
    public async Task ReplacePerformanceSnapshotAsync(KafkaPerformanceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.KafkaPerformanceSnapshots
            .FirstOrDefaultAsync(item => item.ClusterId == snapshot.ClusterId, cancellationToken);
        var replacement = KafkaPerformanceSnapshotEntity.FromModel(snapshot);
        if (existing is not null)
        {
            dbContext.Entry(existing).CurrentValues.SetValues(replacement);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        dbContext.KafkaPerformanceSnapshots.Add(replacement);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two independent UI scopes can race on the first capture. The ClusterId primary key
            // chooses one insert; the losing scope then applies its newer snapshot as an update.
            dbContext.Entry(replacement).State = EntityState.Detached;
            var concurrentlyInserted = await dbContext.KafkaPerformanceSnapshots
                .FirstOrDefaultAsync(item => item.ClusterId == snapshot.ClusterId, cancellationToken);
            if (concurrentlyInserted is null)
            {
                throw;
            }

            dbContext.Entry(concurrentlyInserted).CurrentValues.SetValues(replacement);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<KafkaPerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KafkaPerformanceSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(snapshot => snapshot.ClusterId == clusterId, cancellationToken);
        return entity?.ToModel();
    }
}
