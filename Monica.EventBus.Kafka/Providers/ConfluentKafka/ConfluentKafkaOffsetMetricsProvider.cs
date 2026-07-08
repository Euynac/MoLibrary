using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Confluent Kafka implementation of read-only offset metric sampling.
/// </summary>
internal sealed class ConfluentKafkaOffsetMetricsProvider(IOptions<ModuleEventBusKafkaOption> options)
    : IKafkaOffsetMetricsProvider
{
    private ModuleEventBusKafkaOption Option => options.Value;

    public async Task<KafkaTopicBacklogSnapshot> CaptureTopicBacklogAsync(
        KafkaClusterConfig cluster,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        using var admin = CreateAdminClient(cluster);
        var normalizedTopicName = topicName.Trim();
        var partitions = ResolvePartitions(admin, normalizedTopicName);
        var snapshots = await CaptureTopicBacklogsAsync(admin, cluster.ClusterId, partitions, cancellationToken);
        return snapshots.FirstOrDefault(snapshot => string.Equals(snapshot.TopicName, normalizedTopicName, StringComparison.Ordinal))
               ?? CreateEmptyBacklog(cluster.ClusterId, normalizedTopicName);
    }

    public async Task<IReadOnlyList<KafkaTopicBacklogSnapshot>> CaptureTopicBacklogsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topics);

        var partitions = BuildTopicPartitions(topics);
        if (partitions.Count == 0)
        {
            return topics
                .Select(topic => CreateEmptyBacklog(cluster.ClusterId, topic.TopicName))
                .ToList();
        }

        using var admin = CreateAdminClient(cluster);
        var snapshots = await CaptureTopicBacklogsAsync(admin, cluster.ClusterId, partitions, cancellationToken);
        var snapshotsByTopic = snapshots.ToDictionary(
            snapshot => snapshot.TopicName,
            StringComparer.Ordinal);

        return topics
            .Select(topic => snapshotsByTopic.TryGetValue(topic.TopicName, out var snapshot)
                ? snapshot
                : CreateEmptyBacklog(cluster.ClusterId, topic.TopicName))
            .ToList();
    }

    public async Task<KafkaPerformanceOffsetTotals> CapturePerformanceOffsetTotalsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken = default)
    {
        var partitions = BuildTopicPartitions(topics.Where(topic => !topic.IsInternal));
        if (partitions.Count == 0)
        {
            return new KafkaPerformanceOffsetTotals();
        }

        using var admin = CreateAdminClient(cluster);
        var watermarks = await ReadWatermarksAsync(admin, partitions, cancellationToken);
        var latestOffsets = watermarks.ToDictionary(
            item => item.Key,
            item => item.Value.LatestOffset);
        var consumerOffsets = await ReadConsumerOffsetsAsync(admin, partitions, latestOffsets, consumerGroups, cancellationToken);

        return new KafkaPerformanceOffsetTotals
        {
            TotalLogEndOffset = latestOffsets.Values.Sum(),
            TotalAvailableMessageCount = watermarks.Values.Sum(watermark => watermark.AvailableMessageCount),
            TotalConsumerCommittedOffset = consumerOffsets.TotalCommittedOffset,
            TotalLag = consumerOffsets.TotalLag
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

    private IReadOnlyList<TopicPartition> ResolvePartitions(IAdminClient admin, string topicName)
    {
        var metadata = admin.GetMetadata(topicName, Option.AdminRequestTimeout);
        var topic = metadata.Topics.FirstOrDefault(item =>
            string.Equals(item.Topic, topicName, StringComparison.Ordinal));

        if (topic is null || topic.Error.Code != ErrorCode.NoError)
        {
            throw new InvalidOperationException($"Kafka topic '{topicName}' metadata is not available.");
        }

        return topic.Partitions
            .Where(partition => partition.Error.Code == ErrorCode.NoError)
            .Select(partition => new TopicPartition(topicName, new Partition(partition.PartitionId)))
            .OrderBy(partition => partition.Partition.Value)
            .ToList();
    }

    private static IReadOnlyList<TopicPartition> BuildTopicPartitions(IEnumerable<KafkaTopicSummary> topics)
    {
        return topics
            .Where(topic => topic.Partitions > 0)
            .SelectMany(topic => Enumerable.Range(0, topic.Partitions)
                .Select(partition => new TopicPartition(topic.TopicName, new Partition(partition))))
            .ToList();
    }

    private async Task<IReadOnlyList<KafkaTopicBacklogSnapshot>> CaptureTopicBacklogsAsync(
        IAdminClient admin,
        string clusterId,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        if (partitions.Count == 0)
        {
            return [];
        }

        var capturedAt = DateTimeOffset.UtcNow;
        var watermarks = await ReadWatermarksAsync(admin, partitions, cancellationToken);
        return watermarks
            .Select(item => MapPartitionBacklog(item.Key, item.Value))
            .GroupBy(partition => partition.TopicName, StringComparer.Ordinal)
            .Select(group =>
            {
                var partitionBacklogs = group
                    .Select(partition => partition.Backlog)
                    .OrderBy(partition => partition.Partition)
                    .ToList();

                return new KafkaTopicBacklogSnapshot
                {
                    ClusterId = clusterId,
                    TopicName = group.Key,
                    CapturedAt = capturedAt,
                    TotalAvailableMessageCount = partitionBacklogs.Sum(partition => partition.AvailableMessageCount),
                    Partitions = partitionBacklogs
                };
            })
            .OrderBy(snapshot => snapshot.TopicName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<TopicPartition, PartitionWatermark>> ReadWatermarksAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var earliestOffsets = await ReadOffsetsAsync(admin, partitions, OffsetSpec.Earliest());
        cancellationToken.ThrowIfCancellationRequested();

        var latestOffsets = await ReadOffsetsAsync(admin, partitions, OffsetSpec.Latest());
        cancellationToken.ThrowIfCancellationRequested();

        return partitions
            .Where(partition => earliestOffsets.ContainsKey(partition) && latestOffsets.ContainsKey(partition))
            .ToDictionary(
                partition => partition,
                partition => new PartitionWatermark(earliestOffsets[partition], latestOffsets[partition]));
    }

    private async Task<Dictionary<TopicPartition, long>> ReadOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        OffsetSpec offsetSpec)
    {
        var specs = partitions.Select(partition => new TopicPartitionOffsetSpec
        {
            TopicPartition = partition,
            OffsetSpec = offsetSpec
        });

        var result = await admin.ListOffsetsAsync(specs, new ListOffsetsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        });

        return result.ResultInfos
            .Where(info => info.TopicPartitionOffsetError.Error.Code == ErrorCode.NoError &&
                           info.TopicPartitionOffsetError.Offset.Value >= 0)
            .ToDictionary(
                info => info.TopicPartitionOffsetError.TopicPartition,
                info => info.TopicPartitionOffsetError.Offset.Value);
    }

    private static (string TopicName, KafkaTopicPartitionBacklog Backlog) MapPartitionBacklog(
        TopicPartition topicPartition,
        PartitionWatermark watermark)
    {
        return (
            topicPartition.Topic,
            new KafkaTopicPartitionBacklog
            {
                Partition = topicPartition.Partition.Value,
                EarliestOffset = watermark.EarliestOffset,
                LatestOffset = watermark.LatestOffset,
                AvailableMessageCount = watermark.AvailableMessageCount
            });
    }

    private static KafkaTopicBacklogSnapshot CreateEmptyBacklog(string clusterId, string topicName)
    {
        return new KafkaTopicBacklogSnapshot
        {
            ClusterId = clusterId,
            TopicName = topicName,
            TotalAvailableMessageCount = 0,
            CapturedAt = DateTimeOffset.UtcNow,
            Partitions = []
        };
    }

    private async Task<ConsumerOffsetTotals> ReadConsumerOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken)
    {
        var partitionList = partitions.ToList();
        var requests = consumerGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId))
            .Select(group => new ConsumerGroupTopicPartitions(group.GroupId, partitionList))
            .ToList();
        if (requests.Count == 0)
        {
            return new ConsumerOffsetTotals(null, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var results = await admin.ListConsumerGroupOffsetsAsync(
                requests,
                new ListConsumerGroupOffsetsOptions
                {
                    RequestTimeout = Option.AdminRequestTimeout,
                    RequireStableOffsets = false
                });

            return CalculateConsumerOffsetTotals(results.SelectMany(result => result.Partitions), latestOffsets);
        }
        catch (ListConsumerGroupOffsetsException ex)
        {
            return CalculateConsumerOffsetTotals(ex.Results.SelectMany(result => result.Partitions), latestOffsets);
        }
    }

    private static ConsumerOffsetTotals CalculateConsumerOffsetTotals(
        IEnumerable<TopicPartitionOffsetError> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets)
    {
        long committedTotal = 0;
        long lagTotal = 0;
        var hasConsumerOffsets = false;

        foreach (var partition in partitions)
        {
            if (partition.Error.Code != ErrorCode.NoError || partition.Offset.Value < 0)
            {
                continue;
            }

            hasConsumerOffsets = true;
            committedTotal += partition.Offset.Value;
            lagTotal += CalculateLag(partition, latestOffsets);
        }

        if (!hasConsumerOffsets)
        {
            return new ConsumerOffsetTotals(null, null);
        }

        return new ConsumerOffsetTotals(committedTotal, lagTotal);
    }

    private static long CalculateLag(
        TopicPartitionOffsetError partition,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets)
    {
        return latestOffsets.TryGetValue(partition.TopicPartition, out var latestOffset)
            ? Math.Max(0, latestOffset - partition.Offset.Value)
            : 0;
    }

    private sealed record ConsumerOffsetTotals(long? TotalCommittedOffset, long? TotalLag);

    private sealed record PartitionWatermark(long EarliestOffset, long LatestOffset)
    {
        public long AvailableMessageCount => Math.Max(0, LatestOffset - EarliestOffset);
    }
}
