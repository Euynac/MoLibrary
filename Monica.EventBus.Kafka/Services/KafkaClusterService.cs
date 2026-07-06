using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates configured Kafka clusters and direct broker probes.
/// </summary>
public sealed class KafkaClusterService(
    IKafkaConsoleRepository repository,
    IKafkaAdminProvider adminProvider,
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
        await adminProvider.TestConnectionAsync(cluster, cancellationToken);
        return await BuildSummaryAsync(cluster, refreshBrokerMetadata: true, cancellationToken);
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

        try
        {
            var brokers = await adminProvider.ListBrokersAsync(summary.Config, cancellationToken);
            var topics = await adminProvider.ListTopicsAsync(summary.Config, cancellationToken);
            var groups = await adminProvider.ListConsumerGroupsAsync(summary.Config, cancellationToken);

            summary.IsReachable = brokers.Count > 0;
            summary.BrokerCount = brokers.Count;
            summary.TopicCount = topics.Count;
            summary.ConsumerGroupCount = groups.Count;
        }
        catch (Exception ex)
        {
            summary.IsReachable = false;
            summary.ErrorMessage = ex.Message;
        }

        return summary;
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
