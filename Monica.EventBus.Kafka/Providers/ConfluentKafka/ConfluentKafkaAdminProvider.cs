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
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = CreateAdminClient(cluster);
        var result = await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.DescribeClusterAsync(BuildDescribeClusterOptions()),
            cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = CreateAdminClient(cluster);
        var result = await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.DescribeClusterAsync(BuildDescribeClusterOptions()),
            cancellationToken);
        return MapNodes(result.Nodes);
    }

    public Task<IReadOnlyList<KafkaTopicSummary>> ListTopicsAsync(
        KafkaClusterConfig cluster,
        CancellationToken cancellationToken = default)
    {
        return ListTopicsCoreAsync(cluster, includeConfigurations: true, cancellationToken);
    }

    public Task<IReadOnlyList<KafkaTopicSummary>> ListTopicMetadataAsync(
        KafkaClusterConfig cluster,
        CancellationToken cancellationToken = default)
    {
        return ListTopicsCoreAsync(cluster, includeConfigurations: false, cancellationToken);
    }

    private async Task<IReadOnlyList<KafkaTopicSummary>> ListTopicsCoreAsync(
        KafkaClusterConfig cluster,
        bool includeConfigurations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = CreateAdminClient(cluster);
        var metadata = admin.GetMetadata(Option.AdminRequestTimeout);
        var topicMetadata = metadata.Topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic.Topic))
            .GroupBy(topic => topic.Topic, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(topic => topic.Topic, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topicNames = topicMetadata
            .Where(topic => topic.Error.Code == ErrorCode.NoError)
            .Select(topic => topic.Topic)
            .ToList();
        var retention = includeConfigurations
            ? await DescribeTopicRetentionsAsync(admin, topicNames, cancellationToken)
            : [];

        var topics = topicMetadata
            .Select(topic => new KafkaTopicSummary
            {
                TopicName = topic.Topic,
                Partitions = topic.Partitions.Count,
                ReplicationFactor = topic.Partitions.Count == 0 ? 0 : topic.Partitions.Max(partition => partition.Replicas.Length),
                IsInternal = topic.Topic.StartsWith("__", StringComparison.Ordinal),
                MetadataError = topic.Error.Code == ErrorCode.NoError ? null : topic.Error.Code.ToString(),
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
        cancellationToken.ThrowIfCancellationRequested();
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

        await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.CreateTopicsAsync([specification], new CreateTopicsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            }),
            cancellationToken);
    }

    public async Task DeleteTopicAsync(KafkaClusterConfig cluster, string topicName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = CreateAdminClient(cluster);
        await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.DeleteTopicsAsync([topicName.Trim()], new DeleteTopicsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            }),
            cancellationToken);
    }

    public async Task ClearTopicMessagesAsync(
        KafkaClusterConfig cluster,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedTopicName = topicName.Trim();
        if (IsInternalTopic(normalizedTopicName))
        {
            throw new InvalidOperationException(
                $"Kafka internal topic '{normalizedTopicName}' cannot be cleared.");
        }

        using var admin = CreateAdminClient(cluster);
        var partitions = ResolveTopicPartitions(admin, normalizedTopicName);
        if (partitions.Count == 0)
        {
            return;
        }

        var latestOffsets = await ReadLatestOffsetsAsync(admin, partitions, cancellationToken);
        if (latestOffsets.Count != partitions.Count)
        {
            throw new InvalidOperationException(
                $"Kafka did not return a latest offset for every partition of topic '{normalizedTopicName}'.");
        }

        await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.DeleteRecordsAsync(
                latestOffsets.Select(item => new TopicPartitionOffset(item.Key, new Offset(item.Value))),
                new DeleteRecordsOptions
                {
                    RequestTimeout = Option.AdminRequestTimeout,
                    OperationTimeout = Option.AdminRequestTimeout
                }),
            cancellationToken);
    }

    public async Task IncreasePartitionsAsync(KafkaClusterConfig cluster, KafkaTopicPartitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);
        if (request.IncreaseTo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Partition count must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var admin = CreateAdminClient(cluster);
        await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.CreatePartitionsAsync(
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
                }),
            cancellationToken);
    }

    public async Task UpdateRetentionAsync(KafkaClusterConfig cluster, KafkaTopicRetentionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);
        if (request.RetentionMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Retention must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
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

        await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.IncrementalAlterConfigsAsync(configs, new IncrementalAlterConfigsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<KafkaConsumerGroupSummary>> ListConsumerGroupsAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = CreateAdminClient(cluster);
        // librdkafka 2.13.0 has a native use-after-free in coordinator-targeted Admin requests
        // when connection setup fails (confluentinc/librdkafka#5397). The legacy group-list API
        // uses a different native path and still provides every field needed by this summary.
        var groups = await KafkaNativeRequestAwaiter.RunBlockingAsync(
            () => admin.ListGroups(Option.AdminRequestTimeout),
            cancellationToken);

        return groups
            .Where(group => group.Error.Code == ErrorCode.NoError &&
                            string.Equals(group.ProtocolType, "consumer", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(group.Group))
            .GroupBy(group => group.Group, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(group => group.Group, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, Option.MaxConsumerGroupsToInspect))
            .Select(group => new KafkaConsumerGroupSummary
            {
                GroupId = group.Group,
                State = group.State,
                MemberCount = group.Members.Count,
                TotalLag = null
            })
            .ToList();
    }

    /// <summary>
    /// Describes one consumer group and maps Kafka's member assignment payload to the provider-neutral
    /// console model.
    /// </summary>
    public async Task<KafkaConsumerGroupDescription> DescribeConsumerGroupAsync(
        KafkaClusterConfig cluster,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var normalizedGroupId = groupId.Trim();
        var description = (await DescribeConsumerGroupsAsync(
                cluster,
                [normalizedGroupId],
                cancellationToken))
            .FirstOrDefault(item => string.Equals(item.GroupId, normalizedGroupId, StringComparison.Ordinal));
        if (description is null)
        {
            throw new InvalidOperationException($"Kafka consumer group '{normalizedGroupId}' was not returned by the broker.");
        }

        if (!string.IsNullOrWhiteSpace(description.ErrorMessage))
        {
            throw new InvalidOperationException(
                $"Kafka consumer group '{normalizedGroupId}' could not be described: {description.ErrorMessage}");
        }

        return description;
    }

    /// <summary>
    /// Describes consumer groups in one Kafka admin request and preserves per-group diagnostics.
    /// </summary>
    public async Task<IReadOnlyList<KafkaConsumerGroupDescription>> DescribeConsumerGroupsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedGroupIds = groupIds
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .Select(groupId => groupId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalizedGroupIds.Count == 0)
        {
            return [];
        }

        using var admin = CreateAdminClient(cluster);
        var result = await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.DescribeConsumerGroupsAsync(
                normalizedGroupIds,
                new DescribeConsumerGroupsOptions
                {
                    RequestTimeout = Option.AdminRequestTimeout,
                    IncludeAuthorizedOperations = false
                }),
            cancellationToken);

        var returnedByGroupId = result.ConsumerGroupDescriptions
            .Where(description => !string.IsNullOrWhiteSpace(description.GroupId))
            .GroupBy(description => description.GroupId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return normalizedGroupIds
            .Select(groupId => returnedByGroupId.TryGetValue(groupId, out var description)
                ? MapConsumerGroupDescription(description)
                : new KafkaConsumerGroupDescription
                {
                    GroupId = groupId,
                    ErrorMessage = "The broker did not return a description for this consumer group."
                })
            .ToList();
    }

    private static KafkaConsumerGroupDescription MapConsumerGroupDescription(
        ConsumerGroupDescription description)
    {
        var errorMessage = description.Error.Code == ErrorCode.NoError
            ? null
            : string.IsNullOrWhiteSpace(description.Error.Reason)
                ? description.Error.Code.ToString()
                : $"{description.Error.Code}: {description.Error.Reason}";
        return new KafkaConsumerGroupDescription
        {
            GroupId = description.GroupId,
            State = description.State.ToString(),
            ErrorMessage = errorMessage,
            Members = errorMessage is not null
                ? []
                : description.Members
                    .Select(member => new KafkaConsumerGroupMemberAssignment
                    {
                        ConsumerId = member.ConsumerId ?? string.Empty,
                        Host = member.Host ?? string.Empty,
                        ClientId = member.ClientId ?? string.Empty,
                        Partitions = member.Assignment?.TopicPartitions
                            ?.Select(partition => new KafkaConsumerPartitionAssignment
                            {
                                TopicName = partition.Topic,
                                Partition = partition.Partition.Value
                            })
                            .OrderBy(partition => partition.TopicName, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(partition => partition.Partition)
                            .ToList() ?? []
                    })
                    .OrderBy(member => member.ConsumerId, StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
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

    private IReadOnlyList<TopicPartition> ResolveTopicPartitions(IAdminClient admin, string topicName)
    {
        var metadata = admin.GetMetadata(topicName, Option.AdminRequestTimeout);
        var topic = metadata.Topics.FirstOrDefault(item =>
            string.Equals(item.Topic, topicName, StringComparison.Ordinal));
        if (topic is null || topic.Error.Code != ErrorCode.NoError)
        {
            var reason = topic?.Error.Reason;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(reason)
                    ? $"Kafka topic '{topicName}' metadata is not available."
                    : $"Kafka topic '{topicName}' metadata is not available: {reason}");
        }

        return topic.Partitions
            .Where(partition => partition.Error.Code == ErrorCode.NoError)
            .Select(partition => new TopicPartition(topicName, new Partition(partition.PartitionId)))
            .OrderBy(partition => partition.Partition.Value)
            .ToList();
    }

    private async Task<Dictionary<TopicPartition, long>> ReadLatestOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        var offsets = new Dictionary<TopicPartition, long>();
        var batchSize = Math.Max(1, Option.OffsetQueryBatchSize);
        foreach (var batch in partitions.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await KafkaNativeRequestAwaiter.AwaitAsync(
                admin.ListOffsetsAsync(
                    batch.Select(partition => new TopicPartitionOffsetSpec
                    {
                        TopicPartition = partition,
                        OffsetSpec = OffsetSpec.Latest()
                    }),
                    new ListOffsetsOptions
                    {
                        RequestTimeout = Option.AdminRequestTimeout
                    }),
                cancellationToken);

            foreach (var item in result.ResultInfos)
            {
                var offset = item.TopicPartitionOffsetError;
                if (offset.Error.Code != ErrorCode.NoError || offset.Offset.Value < 0)
                {
                    throw new InvalidOperationException(
                        $"Kafka could not read the latest offset for {offset.TopicPartition}: {offset.Error.Reason}");
                }

                offsets[offset.TopicPartition] = offset.Offset.Value;
            }
        }

        return offsets;
    }

    private DescribeClusterOptions BuildDescribeClusterOptions()
    {
        return new DescribeClusterOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        };
    }

    private static bool IsInternalTopic(string topicName)
    {
        return topicName.StartsWith("__", StringComparison.Ordinal);
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

        var retentions = new Dictionary<string, long?>(StringComparer.Ordinal);
        var batchSize = Math.Max(1, Option.TopicConfigQueryBatchSize);
        foreach (var batch in topicNames.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var resources = batch.Select(topicName => new ConfigResource
                {
                    Type = ResourceType.Topic,
                    Name = topicName
                });
                var result = await KafkaNativeRequestAwaiter.AwaitAsync(
                    admin.DescribeConfigsAsync(resources, new DescribeConfigsOptions
                    {
                        RequestTimeout = Option.AdminRequestTimeout
                    }),
                    cancellationToken);

                foreach (var item in result)
                {
                    retentions[item.ConfigResource.Name] = item.Entries.TryGetValue("retention.ms", out var entry) &&
                                                           long.TryParse(entry.Value, out var value)
                        ? value
                        : null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // A provider-side timeout for one configuration batch should not hide the topic
                // inventory. The caller will render retention as unknown for that batch.
            }
            catch
            {
                // Topic configuration is optional diagnostic enrichment. Keep metadata rows when
                // ACLs or broker limits prevent reading one batch.
            }
        }

        return retentions;
    }
}
