using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates configured Kafka clusters and direct broker probes.
/// </summary>
public sealed class KafkaClusterService(
    IKafkaConsoleRepository repository,
    IKafkaAdminProvider adminProvider,
    KafkaBootstrapEndpointProbe bootstrapEndpointProbe,
    IOptions<ModuleEventBusKafkaOption> options)
{
    public async Task<IReadOnlyList<KafkaClusterSummary>> ListClustersAsync(CancellationToken cancellationToken = default)
    {
        var clusters = await GetEffectiveClustersAsync(cancellationToken);
        var summaries = new List<KafkaClusterSummary>(clusters.Count);

        foreach (var cluster in clusters)
        {
            summaries.Add(await BuildSummaryAsync(cluster, refreshBrokerMetadata: false, cancellationToken));
        }

        return summaries
            .OrderBy(summary => summary.Config.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<KafkaClusterConfig> GetRequiredClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);

        var clusters = await GetEffectiveClustersAsync(cancellationToken);
        return clusters.FirstOrDefault(cluster => string.Equals(cluster.ClusterId, clusterId, StringComparison.Ordinal))
               ?? throw new KeyNotFoundException($"Kafka cluster '{clusterId}' was not found.");
    }

    /// <summary>
    /// Gets a cluster that can be used for direct Kafka administration after a managed endpoint preflight.
    /// </summary>
    /// <param name="clusterId">Cluster identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reachable direct Kafka cluster configuration.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the cluster does not expose direct Kafka access or none of its bootstrap endpoints is reachable.
    /// </exception>
    public async Task<KafkaClusterConfig> GetRequiredDirectAdminClusterAsync(
        string clusterId,
        CancellationToken cancellationToken = default)
    {
        var cluster = await GetRequiredClusterAsync(clusterId, cancellationToken);
        await EnsureDirectKafkaAccessAsync(cluster, cancellationToken);
        return cluster;
    }

    /// <summary>
    /// Verifies that a cluster exposes direct Kafka access and at least one bootstrap endpoint accepts TCP connections.
    /// </summary>
    /// <param name="cluster">Cluster configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when direct Kafka access is unavailable or endpoint preflight fails.
    /// </exception>
    public async Task EnsureDirectKafkaAccessAsync(
        KafkaClusterConfig cluster,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' is visible but direct admin access is not configured.");
        }

        var preflight = await bootstrapEndpointProbe.ProbeAsync(cluster, cancellationToken);
        if (!preflight.IsReachable)
        {
            throw new InvalidOperationException(
                preflight.ErrorMessage ?? $"Kafka cluster '{cluster.ClusterId}' is not reachable.");
        }
    }

    public async Task<KafkaClusterSummary> UpsertClusterAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeForPersistence(cluster);
        await repository.UpsertClusterAsync(normalized, cancellationToken);
        return await BuildSummaryAsync(normalized, refreshBrokerMetadata: false, cancellationToken);
    }

    public Task DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        return repository.DeleteClusterAsync(clusterId.Trim(), cancellationToken);
    }

    public async Task<KafkaClusterSummary> TestConnectionAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = await GetRequiredClusterAsync(clusterId, cancellationToken);
        var summary = await BuildSummaryAsync(cluster, refreshBrokerMetadata: true, cancellationToken);
        if (!summary.IsReachable)
        {
            summary.ErrorMessage ??= $"Kafka cluster '{cluster.ClusterId}' is not reachable.";
        }

        return summary;
    }

    public async Task<IReadOnlyList<KafkaClusterConfig>> GetEffectiveClustersAsync(CancellationToken cancellationToken = default)
    {
        var clusters = new Dictionary<string, KafkaClusterConfig>(StringComparer.Ordinal);

        foreach (var configuredCluster in options.Value.ConfiguredClusters)
        {
            var normalized = configuredCluster.Clone().Normalize();
            clusters[normalized.ClusterId] = normalized;
        }

        if (options.Value.DirectEventBusCluster is not null)
        {
            var directCluster = options.Value.DirectEventBusCluster.Clone().Normalize();
            clusters[directCluster.ClusterId] = directCluster;
        }

        foreach (var persistedCluster in await repository.GetClustersAsync(cancellationToken))
        {
            var normalized = persistedCluster.Clone().Normalize();
            clusters[normalized.ClusterId] = normalized;
        }

        return clusters.Values
            .OrderBy(cluster => cluster.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<KafkaClusterSummary> BuildSummaryAsync(
        KafkaClusterConfig cluster,
        bool refreshBrokerMetadata,
        CancellationToken cancellationToken)
    {
        var summary = new KafkaClusterSummary
        {
            Config = cluster.Clone().Normalize(),
            CapturedAt = DateTimeOffset.UtcNow
        };

        if (!summary.Config.HasDirectKafkaAccess)
        {
            summary.ErrorMessage = summary.Config.IsDaprBacked
                ? "Kafka broker credentials are managed outside Monica; direct admin operations are disabled."
                : "Kafka bootstrap servers or credentials are not available for direct admin operations.";
            return summary;
        }

        if (!refreshBrokerMetadata)
        {
            return summary;
        }

        var preflight = await bootstrapEndpointProbe.ProbeAsync(summary.Config, cancellationToken);
        if (!preflight.IsReachable)
        {
            summary.IsReachable = false;
            summary.ErrorMessage = preflight.ErrorMessage;
            return summary;
        }

        try
        {
            var connection = await adminProvider.TestConnectionAsync(summary.Config, cancellationToken);
            summary.IsReachable = true;
            summary.BrokerCount = connection.Brokers.Count;
            await TryPopulateInventoryCountsAsync(summary, cancellationToken);
        }
        catch (Exception ex)
        {
            summary.IsReachable = false;
            summary.ErrorMessage = BuildConnectionErrorMessage(ex);
        }

        return summary;
    }

    private async Task TryPopulateInventoryCountsAsync(
        KafkaClusterSummary summary,
        CancellationToken cancellationToken)
    {
        try
        {
            var topics = await adminProvider.ListTopicsAsync(summary.Config, cancellationToken);
            summary.TopicCount = topics.Count;
        }
        catch (Exception ex)
        {
            summary.ErrorMessage = $"Kafka connection succeeded, but topic metadata could not be read: {ex.GetMessageRecursively()}";
        }

        try
        {
            var groups = await adminProvider.ListConsumerGroupsAsync(summary.Config, cancellationToken);
            summary.ConsumerGroupCount = groups.Count;
        }
        catch (Exception ex)
        {
            var groupError = $"consumer group metadata could not be read: {ex.GetMessageRecursively()}";
            summary.ErrorMessage = string.IsNullOrWhiteSpace(summary.ErrorMessage)
                ? $"Kafka connection succeeded, but {groupError}"
                : $"{summary.ErrorMessage}; {groupError}";
        }
    }

    private static string BuildConnectionErrorMessage(Exception exception)
    {
        var message = exception.GetMessageRecursively();
        if (message.Contains("Local: Broker transport failure", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message}. TCP reached the bootstrap endpoint, but Kafka metadata could not be read. " +
                   "Check the broker advertised.listeners value, security protocol, SASL/SSL settings, and whether the advertised broker address is reachable from Monica.";
        }

        return message;
    }

    private static KafkaClusterConfig NormalizeForPersistence(KafkaClusterConfig cluster)
    {
        var normalized = cluster.Clone().Normalize();
        if (string.IsNullOrWhiteSpace(normalized.DisplayName))
        {
            throw new ArgumentException("Kafka cluster display name is required.", nameof(cluster));
        }

        if (string.IsNullOrWhiteSpace(normalized.BootstrapServers) && !normalized.CredentialsManagedExternally)
        {
            throw new ArgumentException("Kafka bootstrap servers are required when credentials are managed by Monica.", nameof(cluster));
        }

        normalized.UpdatedAt = DateTimeOffset.UtcNow;
        return normalized;
    }
}
