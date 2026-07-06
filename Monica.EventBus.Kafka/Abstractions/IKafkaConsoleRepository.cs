using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Abstractions;

/// <summary>
/// Stores Kafka console cluster configuration and sampled performance snapshots.
/// </summary>
/// <remarks>
/// Implementations must be safe for concurrent reads and writes because UI requests and hosted
/// samplers can access the repository at the same time. Implementations should not throw when a
/// delete targets a missing cluster.
/// </remarks>
public interface IKafkaConsoleRepository
{
    /// <summary>
    /// Gets all configured Kafka clusters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configured clusters.</returns>
    Task<IReadOnlyList<KafkaClusterConfig>> GetClustersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one configured Kafka cluster.
    /// </summary>
    /// <param name="clusterId">Cluster identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cluster when found; otherwise <see langword="null"/>.</returns>
    Task<KafkaClusterConfig?> GetClusterAsync(string clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates one configured Kafka cluster.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertClusterAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one configured Kafka cluster and any cluster-owned sampled snapshots.
    /// </summary>
    /// <param name="clusterId">Cluster identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a sampled performance snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SavePerformanceSnapshotAsync(KafkaPerformanceSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest sampled performance snapshot for one cluster.
    /// </summary>
    /// <param name="clusterId">Cluster identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest snapshot when found; otherwise <see langword="null"/>.</returns>
    Task<KafkaPerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent sampled performance snapshots for one cluster.
    /// </summary>
    /// <param name="clusterId">Cluster identifier.</param>
    /// <param name="limit">Maximum number of snapshots to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Snapshots ordered from oldest to newest.</returns>
    Task<IReadOnlyList<KafkaPerformanceSnapshot>> GetPerformanceSnapshotsAsync(
        string clusterId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes sampled performance snapshots older than the specified timestamp.
    /// </summary>
    /// <param name="olderThanUtc">Exclusive UTC cutoff.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeletePerformanceSnapshotsOlderThanAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default);
}
