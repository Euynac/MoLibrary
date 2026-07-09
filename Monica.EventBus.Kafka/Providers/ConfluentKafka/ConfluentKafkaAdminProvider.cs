using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Confluent Kafka implementation of Kafka console administration operations.
/// </summary>
internal sealed class ConfluentKafkaAdminProvider(IOptions<ModuleEventBusKafkaOption> options) : IKafkaAdminProvider
{
    private ModuleEventBusKafkaOption Option => options.Value;

    public async Task<KafkaConnectionTestResult> TestConnectionAsync(
        KafkaClusterConfig cluster,
        CancellationToken cancellationToken = default)
    {
        using var admin = CreateAdminClient(cluster);
        var result = await admin.DescribeClusterAsync(BuildDescribeClusterOptions()).WaitAsync(cancellationToken);
        var brokers = MapNodes(result.Nodes);
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException($"Kafka cluster '{cluster.ClusterId}' returned no brokers.");
        }

        return new KafkaConnectionTestResult
        {
            Brokers = brokers
        };
    }

    public async Task<IReadOnlyList<KafkaBrokerInfo>> ListBrokersAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        using var admin = CreateAdminClient(cluster);
        var result = await admin.DescribeClusterAsync(BuildDescribeClusterOptions()).WaitAsync(cancellationToken);
        return MapNodes(result.Nodes);
    }

    public async Task<IReadOnlyList<KafkaTopicSummary>> ListTopicsAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        using var admin = CreateAdminClient(cluster);
        var metadata = admin.GetMetadata(Option.AdminRequestTimeout);
        var topicNames = metadata.Topics
            .Where(topic => topic.Error.Code == ErrorCode.NoError)
            .Select(topic => topic.Topic)
            .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var retention = await DescribeTopicRetentionsAsync(admin, topicNames, cancellationToken);
        var topics = metadata.Topics
            .Where(topic => topic.Error.Code == ErrorCode.NoError)
            .Select(topic => new KafkaTopicSummary
            {
                TopicName = topic.Topic,
                Partitions = topic.Partitions.Count,
                ReplicationFactor = topic.Partitions.Count == 0 ? 0 : topic.Partitions.Max(partition => partition.Replicas.Length),
                IsInternal = topic.Topic.StartsWith("__", StringComparison.Ordinal),
                RetentionMs = retention.GetValueOrDefault(topic.Topic)
            })
            .OrderBy(topic => topic.TopicName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return topics;
    }

    public async Task CreateTopicAsync(KafkaClusterConfig cluster, KafkaTopicCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);
        using var admin = CreateAdminClient(cluster);

        var specification = new TopicSpecification
        {
            Name = request.TopicName.Trim(),
            NumPartitions = Math.Max(1, request.Partitions),
            ReplicationFactor = Math.Max((short)1, request.ReplicationFactor),
            Configs = request.RetentionMs is > 0
                ? new Dictionary<string, string> { ["retention.ms"] = request.RetentionMs.Value.ToString() }
                : null
        };

        await admin.CreateTopicsAsync([specification], new CreateTopicsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        });
    }

    public async Task DeleteTopicAsync(KafkaClusterConfig cluster, string topicName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        using var admin = CreateAdminClient(cluster);
        await admin.DeleteTopicsAsync([topicName.Trim()], new DeleteTopicsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        });
    }

    public async Task IncreasePartitionsAsync(KafkaClusterConfig cluster, KafkaTopicPartitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);
        if (request.IncreaseTo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Partition count must be greater than zero.");
        }

        using var admin = CreateAdminClient(cluster);
        await admin.CreatePartitionsAsync(
            [
                new PartitionsSpecification
                {
                    Topic = request.TopicName.Trim(),
                    IncreaseTo = request.IncreaseTo
                }
            ],
            new CreatePartitionsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            });
    }

    public async Task UpdateRetentionAsync(KafkaClusterConfig cluster, KafkaTopicRetentionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);
        if (request.RetentionMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Retention must be greater than zero.");
        }

        using var admin = CreateAdminClient(cluster);
        var resource = new ConfigResource
        {
            Type = ResourceType.Topic,
            Name = request.TopicName.Trim()
        };
        var configs = new Dictionary<ConfigResource, List<ConfigEntry>>
        {
            [resource] =
            [
                new ConfigEntry
                {
                    Name = "retention.ms",
                    Value = request.RetentionMs.ToString(),
                    IncrementalOperation = AlterConfigOpType.Set
                }
            ]
        };

        await admin.IncrementalAlterConfigsAsync(configs, new IncrementalAlterConfigsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        });
    }

    public async Task<IReadOnlyList<KafkaConsumerGroupSummary>> ListConsumerGroupsAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        using var admin = CreateAdminClient(cluster);
        var result = await admin.ListConsumerGroupsAsync(new ListConsumerGroupsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        });

        var groupIds = result.Valid
            .Select(group => group.GroupId)
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(groupId => groupId, StringComparer.OrdinalIgnoreCase)
            .Take(Option.MaxConsumerGroupsToDescribe)
            .ToList();

        if (groupIds.Count == 0)
        {
            return [];
        }

        var descriptions = await admin.DescribeConsumerGroupsAsync(groupIds, new DescribeConsumerGroupsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        });

        return descriptions.ConsumerGroupDescriptions
            .Select(group => new KafkaConsumerGroupSummary
            {
                GroupId = group.GroupId,
                State = group.State.ToString(),
                MemberCount = group.Members.Count,
                TotalLag = null
            })
            .OrderBy(group => group.GroupId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IAdminClient CreateAdminClient(KafkaClusterConfig cluster)
    {
        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' does not expose direct broker credentials to Monica.");
        }

        return new AdminClientBuilder(KafkaClientConfigFactory.BuildAdminConfig(cluster, Option)).Build();
    }

    private DescribeClusterOptions BuildDescribeClusterOptions()
    {
        return new DescribeClusterOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        };
    }

    private static List<KafkaBrokerInfo> MapNodes(IReadOnlyList<Node> nodes)
    {
        return nodes
            .Select(node => new KafkaBrokerInfo
            {
                BrokerId = node.Id,
                Host = node.Host,
                Port = node.Port
            })
            .OrderBy(broker => broker.BrokerId)
            .ToList();
    }

    private static List<KafkaBrokerInfo> MapBrokers(Metadata metadata)
    {
        return metadata.Brokers
            .Select(broker => new KafkaBrokerInfo
            {
                BrokerId = broker.BrokerId,
                Host = broker.Host,
                Port = broker.Port
            })
            .OrderBy(broker => broker.BrokerId)
            .ToList();
    }

    private async Task<Dictionary<string, long?>> DescribeTopicRetentionsAsync(
        IAdminClient admin,
        IReadOnlyList<string> topicNames,
        CancellationToken cancellationToken)
    {
        if (topicNames.Count == 0)
        {
            return [];
        }

        try
        {
            var resources = topicNames.Select(topicName => new ConfigResource
            {
                Type = ResourceType.Topic,
                Name = topicName
            });
            var result = await admin.DescribeConfigsAsync(resources, new DescribeConfigsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            });

            return result.ToDictionary(
                item => item.ConfigResource.Name,
                item => item.Entries.TryGetValue("retention.ms", out var entry) && long.TryParse(entry.Value, out var value)
                    ? value
                    : (long?)null,
                StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }
}
