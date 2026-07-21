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
            return [];
        }

        using var admin = CreateAdminClient(cluster);
        var snapshots = await CaptureTopicBacklogsAsync(admin, cluster.ClusterId, partitions, cancellationToken);
        var snapshotsByTopic = snapshots.ToDictionary(
            snapshot => snapshot.TopicName,
            StringComparer.Ordinal);

        return topics
            .Where(topic => snapshotsByTopic.ContainsKey(topic.TopicName))
            .Select(topic => snapshotsByTopic[topic.TopicName])
            .ToList();
    }

    public async Task<KafkaPerformanceOffsetTotals> CapturePerformanceOffsetTotalsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken = default)
    {
        var hasUnavailableTopicMetadata = topics.Any(topic => !topic.IsMetadataAvailable);
        var allPartitions = BuildTopicPartitions(topics);
        if (allPartitions.Count == 0)
        {
            return new KafkaPerformanceOffsetTotals
            {
                IsComplete = !hasUnavailableTopicMetadata,
                TotalLogEndOffset = 0,
                TotalAvailableMessageCount = hasUnavailableTopicMetadata ? null : 0
            };
        }

        using var admin = CreateAdminClient(cluster);
        var watermarks = await ReadWatermarksAsync(admin, allPartitions, cancellationToken);
        var applicationPartitions = BuildTopicPartitions(topics.Where(topic => !topic.IsInternal));
        var applicationWatermarks = applicationPartitions
            .Where(watermarks.ContainsKey)
            .ToDictionary(partition => partition, partition => watermarks[partition]);
        var latestOffsets = applicationWatermarks.ToDictionary(
            item => item.Key,
            item => item.Value.LatestOffset);
        var hasCompleteApplicationOffsets = !hasUnavailableTopicMetadata &&
                                            applicationWatermarks.Count == applicationPartitions.Count;
        var hasCompleteWatermarks = !hasUnavailableTopicMetadata &&
                                    watermarks.Count == allPartitions.Count;
        var consumerOffsets = !hasCompleteApplicationOffsets || latestOffsets.Count == 0
            ? new ConsumerOffsetTotals(null, null)
            : await ReadConsumerOffsetsAsync(admin, applicationPartitions, latestOffsets, consumerGroups, cancellationToken);

        return new KafkaPerformanceOffsetTotals
        {
            IsComplete = hasCompleteApplicationOffsets && hasCompleteWatermarks,
            TotalLogEndOffset = hasCompleteApplicationOffsets ? latestOffsets.Values.Sum() : 0,
            TotalAvailableMessageCount = hasCompleteWatermarks
                ? watermarks.Values.Sum(watermark => watermark.AvailableMessageCount)
                : null,
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
            .Where(topic => topic.IsMetadataAvailable && topic.Partitions > 0)
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

        var earliestOffsets = await ReadOffsetsInBatchesAsync(admin, partitions, OffsetSpec.Earliest(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var latestOffsets = await ReadOffsetsInBatchesAsync(admin, partitions, OffsetSpec.Latest(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return partitions
            .Where(partition => earliestOffsets.ContainsKey(partition) && latestOffsets.ContainsKey(partition))
            .ToDictionary(
                partition => partition,
                partition => new PartitionWatermark(earliestOffsets[partition], latestOffsets[partition]));
    }

    private async Task<Dictionary<TopicPartition, long>> ReadOffsetsInBatchesAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        OffsetSpec offsetSpec,
        CancellationToken cancellationToken)
    {
        var offsets = new Dictionary<TopicPartition, long>();
        var batchSize = Math.Max(1, Option.OffsetQueryBatchSize);
        foreach (var batch in partitions.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchOffsets = await ReadOffsetsAsync(admin, batch, offsetSpec, cancellationToken);
            foreach (var (partition, offset) in batchOffsets)
            {
                offsets[partition] = offset;
            }
        }

        return offsets;
    }

    private async Task<Dictionary<TopicPartition, long>> ReadOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        OffsetSpec offsetSpec,
        CancellationToken cancellationToken)
    {
        var specs = partitions.Select(partition => new TopicPartitionOffsetSpec
        {
            TopicPartition = partition,
            OffsetSpec = offsetSpec
        });

        var result = await admin.ListOffsetsAsync(specs, new ListOffsetsOptions
        {
            RequestTimeout = Option.AdminRequestTimeout
        }).WaitAsync(cancellationToken);

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
        var groups = consumerGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId))
            .ToList();
        if (groups.Count == 0)
        {
            return new ConsumerOffsetTotals(null, null);
        }

        // Confluent.Kafka requires exactly one ConsumerGroupTopicPartitions item per
        // ListConsumerGroupOffsetsAsync call. Query groups with bounded parallelism and split very
        // large partition lists so one oversized request cannot invalidate the whole sample.
        var parallelism = Math.Max(1, Option.ConsumerGroupOffsetParallelism);
        using var gate = new SemaphoreSlim(parallelism, parallelism);
        var groupTasks = groups.Select(async group =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await ReadConsumerGroupOffsetsAsync(
                    admin,
                    group.GroupId,
                    partitionList,
                    latestOffsets,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        var groupTotals = await Task.WhenAll(groupTasks);
        var committedTotal = 0L;
        var lagTotal = 0L;
        var hasConsumerOffsets = false;
        foreach (var total in groupTotals)
        {
            if (!total.TotalCommittedOffset.HasValue)
            {
                continue;
            }

            hasConsumerOffsets = true;
            committedTotal += total.TotalCommittedOffset.GetValueOrDefault();
            lagTotal += total.TotalLag.GetValueOrDefault();
        }

        return hasConsumerOffsets
            ? new ConsumerOffsetTotals(committedTotal, lagTotal)
            : new ConsumerOffsetTotals(null, null);
    }

    private async Task<ConsumerOffsetTotals> ReadConsumerGroupOffsetsAsync(
        IAdminClient admin,
        string groupId,
        IReadOnlyList<TopicPartition> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets,
        CancellationToken cancellationToken)
    {
        var committedTotal = 0L;
        var lagTotal = 0L;
        var hasConsumerOffsets = false;
        var batchSize = Math.Max(1, Option.OffsetQueryBatchSize);

        foreach (var batch in partitions.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await admin.ListConsumerGroupOffsetsAsync(
                    [new ConsumerGroupTopicPartitions(groupId, batch.ToList())],
                    new ListConsumerGroupOffsetsOptions
                    {
                        RequestTimeout = Option.AdminRequestTimeout,
                        RequireStableOffsets = false
                    }).WaitAsync(cancellationToken);

                var total = CalculateConsumerOffsetTotals(result.SelectMany(item => item.Partitions), latestOffsets);
                committedTotal += total.TotalCommittedOffset.GetValueOrDefault();
                lagTotal += total.TotalLag.GetValueOrDefault();
                hasConsumerOffsets |= total.TotalCommittedOffset.HasValue;
            }
            catch (ListConsumerGroupOffsetsException ex)
            {
                var total = CalculateConsumerOffsetTotals(ex.Results.SelectMany(item => item.Partitions), latestOffsets);
                committedTotal += total.TotalCommittedOffset.GetValueOrDefault();
                lagTotal += total.TotalLag.GetValueOrDefault();
                hasConsumerOffsets |= total.TotalCommittedOffset.HasValue;
            }
        }

        return hasConsumerOffsets
            ? new ConsumerOffsetTotals(committedTotal, lagTotal)
            : new ConsumerOffsetTotals(null, null);
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

        return hasConsumerOffsets
            ? new ConsumerOffsetTotals(committedTotal, lagTotal)
            : new ConsumerOffsetTotals(null, null);
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
