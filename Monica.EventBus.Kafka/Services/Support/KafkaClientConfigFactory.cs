using Confluent.Kafka;
using Monica.EventBus.Kafka.Models;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services.Support;

/// <summary>
/// Builds Confluent Kafka client configuration from Monica Kafka cluster models.
/// </summary>
internal static class KafkaClientConfigFactory
{
    public static AdminClientConfig BuildAdminConfig(KafkaClusterConfig cluster, ModuleEventBusKafkaOption option)
    {
        var config = new AdminClientConfig();
        ApplyCommon(config, cluster, option);
        return config;
    }

    public static ProducerConfig BuildProducerConfig(KafkaClusterConfig cluster, ModuleEventBusKafkaOption option)
    {
        var config = new ProducerConfig
        {
            Acks = Acks.All
        };
        ApplyCommon(config, cluster, option);
        return config;
    }

    public static ConsumerConfig BuildConsumerConfig(KafkaClusterConfig cluster, ModuleEventBusKafkaOption option, string? serviceKey)
    {
        var config = new ConsumerConfig
        {
            GroupId = BuildConsumerGroupId(option, serviceKey),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        ApplyCommon(config, cluster, option);
        return config;
    }

    public static ConsumerConfig BuildReadOnlyConsumerConfig(KafkaClusterConfig cluster, ModuleEventBusKafkaOption option, string serviceKey)
    {
        var config = new ConsumerConfig
        {
            GroupId = $"{BuildConsumerGroupId(option, serviceKey)}-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = false
        };
        ApplyCommon(config, cluster, option);
        return config;
    }

    private static void ApplyCommon(ClientConfig config, KafkaClusterConfig cluster, ModuleEventBusKafkaOption option)
    {
        var normalized = cluster.Clone().Normalize();
        if (string.IsNullOrWhiteSpace(normalized.BootstrapServers))
        {
            throw new InvalidOperationException($"Kafka cluster '{normalized.ClusterId}' does not define bootstrap servers.");
        }

        config.BootstrapServers = normalized.BootstrapServers;
        config.ClientId = normalized.ClientId ?? option.ClientId;
        config.SocketTimeoutMs = (int)option.AdminRequestTimeout.TotalMilliseconds;

        if (TryParseEnum<SecurityProtocol>(normalized.SecurityProtocol, out var securityProtocol))
        {
            config.SecurityProtocol = securityProtocol;
        }

        if (TryParseEnum<SaslMechanism>(normalized.SaslMechanism, out var saslMechanism))
        {
            config.SaslMechanism = saslMechanism;
        }

        if (!string.IsNullOrWhiteSpace(normalized.SaslUsername))
        {
            config.SaslUsername = normalized.SaslUsername;
        }

        if (!string.IsNullOrWhiteSpace(normalized.SaslPassword))
        {
            config.SaslPassword = normalized.SaslPassword;
        }

        if (!string.IsNullOrWhiteSpace(normalized.SslCaLocation))
        {
            config.SslCaLocation = normalized.SslCaLocation;
        }
    }

    private static string BuildConsumerGroupId(ModuleEventBusKafkaOption option, string? serviceKey)
    {
        return string.IsNullOrWhiteSpace(serviceKey)
            ? option.ConsumerGroupId
            : $"{option.ConsumerGroupId}-{serviceKey}";
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum result) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out result);
    }
}
