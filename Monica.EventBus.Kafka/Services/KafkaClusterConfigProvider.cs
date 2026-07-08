using Microsoft.Extensions.Options;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Resolves cluster configuration used by the direct Kafka EventBus provider.
/// </summary>
internal sealed class KafkaClusterConfigProvider(IOptions<ModuleEventBusKafkaOption> options) : IKafkaClusterConfigProvider
{
    public KafkaClusterConfig GetDirectEventBusCluster()
    {
        var cluster = options.Value.DirectEventBusCluster;
        if (cluster is null)
        {
            throw new InvalidOperationException(
                "The native Kafka EventBus provider is enabled, but no direct Kafka cluster has been configured.");
        }

        return cluster.Clone().Normalize();
    }
}
