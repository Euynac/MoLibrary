using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Abstractions;

/// <summary>
/// Resolves the default Kafka cluster used by the native Kafka EventBus provider.
/// </summary>
public interface IKafkaClusterConfigProvider
{
    /// <summary>
    /// Gets the direct EventBus cluster configuration.
    /// </summary>
    /// <returns>The direct Kafka cluster configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no direct Kafka cluster is configured.</exception>
    KafkaClusterConfig GetDirectEventBusCluster();
}
