using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Providers.ConfluentKafka;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Builds Kafka integration status snapshots for direct and Dapr-backed configurations.
/// </summary>
public sealed class KafkaIntegrationService(
    IServiceProvider serviceProvider,
    KafkaClusterService clusterService,
    IOptions<ModuleEventBusKafkaOption> options)
{
    public async Task<KafkaIntegrationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var clusters = await clusterService.GetEffectiveClustersAsync(cancellationToken);
        var activeProvider = serviceProvider.GetService<IDistributedEventBus>();
        var primaryCluster = ResolvePrimaryCluster(clusters);
        var mode = ResolveMode(activeProvider, primaryCluster);

        return new KafkaIntegrationSnapshot
        {
            Mode = mode,
            ActiveProviderName = ResolveProviderName(activeProvider),
            DaprPubSubName = primaryCluster?.DaprPubSubName ?? options.Value.DaprPubSubName,
            PrimaryClusterId = primaryCluster?.ClusterId,
            Capabilities = ResolveCapabilities(mode, primaryCluster),
            Messages = BuildMessages(mode, primaryCluster)
        };
    }

    private KafkaClusterConfig? ResolvePrimaryCluster(IReadOnlyList<KafkaClusterConfig> clusters)
    {
        var option = options.Value;
        if (!string.IsNullOrWhiteSpace(option.PrimaryClusterId))
        {
            var configuredPrimary = clusters.FirstOrDefault(cluster =>
                string.Equals(cluster.ClusterId, option.PrimaryClusterId, StringComparison.Ordinal));
            if (configuredPrimary is not null)
            {
                return configuredPrimary;
            }
        }

        if (option.DirectEventBusCluster is not null)
        {
            return option.DirectEventBusCluster.Clone().Normalize();
        }

        return clusters.FirstOrDefault(cluster => cluster.IsDaprBacked) ?? clusters.FirstOrDefault();
    }

    private KafkaIntegrationMode ResolveMode(IDistributedEventBus? activeProvider, KafkaClusterConfig? primaryCluster)
    {
        if (activeProvider is KafkaEventBusProvider || options.Value.DirectEventBusCluster is not null)
        {
            return KafkaIntegrationMode.DirectKafka;
        }

        if (IsDaprProvider(activeProvider))
        {
            return primaryCluster?.IsDaprBacked == true
                ? KafkaIntegrationMode.DaprKafka
                : KafkaIntegrationMode.DaprNonKafka;
        }

        return primaryCluster is null ? KafkaIntegrationMode.NotConfigured : KafkaIntegrationMode.Unknown;
    }

    private static string ResolveProviderName(IDistributedEventBus? activeProvider)
    {
        if (activeProvider is null)
        {
            return "Not configured";
        }

        return activeProvider switch
        {
            KafkaEventBusProvider => "Kafka",
            _ when IsDaprProvider(activeProvider) => "Dapr",
            _ => activeProvider.GetType().Name
        };
    }

    private static KafkaConsoleCapabilities ResolveCapabilities(KafkaIntegrationMode mode, KafkaClusterConfig? primaryCluster)
    {
        var capabilities = mode is KafkaIntegrationMode.DirectKafka or KafkaIntegrationMode.DaprKafka
            ? KafkaConsoleCapabilities.PublishSubscribe
            : KafkaConsoleCapabilities.None;

        if (primaryCluster?.HasDirectKafkaAccess == true)
        {
            capabilities |= KafkaConsoleCapabilities.TopicAdmin |
                            KafkaConsoleCapabilities.ConsumerGroups |
                            KafkaConsoleCapabilities.PerformanceSampling;
        }

        if (!string.IsNullOrWhiteSpace(primaryCluster?.JmxEndpoint))
        {
            capabilities |= KafkaConsoleCapabilities.JmxMetrics;
        }

        return capabilities;
    }

    private static IReadOnlyList<string> BuildMessages(KafkaIntegrationMode mode, KafkaClusterConfig? primaryCluster)
    {
        var messages = new List<string>();

        if (mode == KafkaIntegrationMode.NotConfigured)
        {
            messages.Add("No Kafka integration has been configured.");
        }
        else if (mode == KafkaIntegrationMode.DaprKafka)
        {
            messages.Add("Dapr remains the active EventBus provider; Kafka is visible as the backing broker integration.");
        }
        else if (mode == KafkaIntegrationMode.DaprNonKafka)
        {
            messages.Add("Dapr is the active EventBus provider, but no Kafka-backed Dapr pub/sub component is declared.");
        }

        if (primaryCluster is not null && !primaryCluster.HasDirectKafkaAccess)
        {
            messages.Add("Direct Kafka administration is unavailable because broker access is not configured in Monica.");
        }

        return messages;
    }

    private static bool IsDaprProvider(IDistributedEventBus? activeProvider)
    {
        return activeProvider?.GetType().FullName?.Contains("Dapr", StringComparison.OrdinalIgnoreCase) == true;
    }
}
