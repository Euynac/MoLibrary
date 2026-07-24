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
        cancellationToken.ThrowIfCancellationRequested();

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
        cancellationToken.ThrowIfCancellationRequested();

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
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(consumerGroups);
        cancellationToken.ThrowIfCancellationRequested();

        // Kafka's internal topics (especially __consumer_offsets) are implementation details, not
        // application traffic. Excluding them before any watermark request keeps dashboard totals
        // and per-topic rates aligned with the topics users can actually operate.
        var applicationTopics = topics
            .Where(topic => !topic.IsInternal)
            .ToList();
        var hasUnavailableTopicMetadata = applicationTopics.Any(topic => !topic.IsMetadataAvailable);
        var applicationPartitions = BuildTopicPartitions(applicationTopics);
        if (applicationPartitions.Count == 0)
        {
            return new KafkaPerformanceOffsetTotals
            {
                IsComplete = !hasUnavailableTopicMetadata,
                TotalLogEndOffset = 0,
                TotalAvailableMessageCount = hasUnavailableTopicMetadata ? null : 0,
                TopicTotals = BuildTopicTotals(
                    applicationTopics,
                    new Dictionary<TopicPartition, PartitionWatermark>())
            };
        }

        Dictionary<TopicPartition, PartitionWatermark> watermarks;
        using (var admin = CreateAdminClient(cluster))
        {
            watermarks = await ReadWatermarksAsync(admin, applicationPartitions, cancellationToken);
        }

        var applicationWatermarks = applicationPartitions
            .Where(watermarks.ContainsKey)
            .ToDictionary(partition => partition, partition => watermarks[partition]);
        var latestOffsets = applicationWatermarks.ToDictionary(
            item => item.Key,
            item => item.Value.LatestOffset);
        var hasCompleteApplicationOffsets = !hasUnavailableTopicMetadata &&
                                            applicationWatermarks.Count == applicationPartitions.Count;
        var consumerOffsets = latestOffsets.Count == 0
            ? ConsumerOffsetTotals.Empty
            : await ReadConsumerOffsetsAsync(
                cluster,
                applicationPartitions,
                latestOffsets,
                consumerGroups,
                cancellationToken);
        var topicTotals = BuildTopicTotals(applicationTopics, applicationWatermarks);
        ApplyConsumerOffsets(topicTotals, consumerOffsets);

        return new KafkaPerformanceOffsetTotals
        {
            IsComplete = hasCompleteApplicationOffsets,
            TotalLogEndOffset = hasCompleteApplicationOffsets ? latestOffsets.Values.Sum() : 0,
            TotalAvailableMessageCount = hasCompleteApplicationOffsets
                ? applicationWatermarks.Values.Sum(watermark => watermark.AvailableMessageCount)
                : null,
            TotalConsumerCommittedOffset = consumerOffsets.TotalCommittedOffset,
            TotalLag = consumerOffsets.TotalLag,
            TopicTotals = topicTotals
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

    private IConsumer<byte[], byte[]> CreateOffsetQueryConsumer(KafkaClusterConfig cluster, string groupId)
    {
        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' does not expose direct broker credentials to Monica.");
        }

        var config = KafkaClientConfigFactory.BuildOffsetQueryConsumerConfig(cluster, Option, groupId);
        return new ConsumerBuilder<byte[], byte[]>(config).Build();
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

    private static List<KafkaTopicPerformanceOffsetTotals> BuildTopicTotals(
        IEnumerable<KafkaTopicSummary> topics,
        IReadOnlyDictionary<TopicPartition, PartitionWatermark> watermarks)
    {
        return topics
            .OrderBy(topic => topic.TopicName, StringComparer.OrdinalIgnoreCase)
            .Select(topic =>
            {
                var expectedPartitions = topic.Partitions > 0
                    ? Enumerable.Range(0, topic.Partitions)
                        .Select(partition => new TopicPartition(topic.TopicName, new Partition(partition)))
                        .ToList()
                    : [];
                var capturedPartitions = expectedPartitions
                    .Where(watermarks.ContainsKey)
                    .ToList();
                var isComplete = topic.IsMetadataAvailable &&
                                  capturedPartitions.Count == expectedPartitions.Count;

                return new KafkaTopicPerformanceOffsetTotals
                {
                    TopicName = topic.TopicName,
                    TotalLogEndOffset = isComplete
                        ? capturedPartitions.Sum(partition => watermarks[partition].LatestOffset)
                        : null,
                    TotalAvailableMessageCount = isComplete
                        ? capturedPartitions.Sum(partition => watermarks[partition].AvailableMessageCount)
                        : null,
                    IsComplete = isComplete
                };
            })
            .ToList();
    }

    private static void ApplyConsumerOffsets(
        IReadOnlyList<KafkaTopicPerformanceOffsetTotals> topicTotals,
        ConsumerOffsetTotals consumerOffsets)
    {
        foreach (var topic in topicTotals)
        {
            if (!consumerOffsets.ByTopic.TryGetValue(topic.TopicName, out var offsets))
            {
                continue;
            }

            topic.TotalConsumerCommittedOffset = offsets.TotalCommittedOffset;
            topic.TotalLag = offsets.TotalLag;
        }
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

        var result = await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.ListOffsetsAsync(specs, new ListOffsetsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            }),
            cancellationToken);

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
        KafkaClusterConfig cluster,
        IReadOnlyList<TopicPartition> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken)
    {
        var partitionList = partitions.ToList();
        var groups = consumerGroups
            .Select(group => group.GroupId?.Trim())
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .Select(groupId => groupId!)
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(0, Option.MaxConsumerGroupsToInspect))
            .ToList();
        if (groups.Count == 0)
        {
            return ConsumerOffsetTotals.Empty;
        }

        // librdkafka 2.13.0 can terminate the process when a coordinator-targeted Admin request
        // fails during connection setup. Query committed offsets through isolated, short-lived
        // read-only consumers instead, and bound their native handles and worker threads.
        var parallelism = Math.Clamp(Option.ConsumerOffsetQueryParallelism, 1, groups.Count);
        using var gate = new SemaphoreSlim(parallelism, parallelism);
        var groupTasks = groups.Select(async groupId =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await KafkaNativeRequestAwaiter.RunBlockingAsync(
                    () => ReadConsumerGroupOffsets(
                        cluster,
                        groupId,
                        partitionList,
                        latestOffsets,
                        cancellationToken),
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        var groupTotals = await Task.WhenAll(groupTasks);
        var accumulator = new ConsumerOffsetAccumulator();
        foreach (var total in groupTotals)
        {
            accumulator.Add(total);
        }

        return accumulator.Build();
    }

    private ConsumerOffsetTotals ReadConsumerGroupOffsets(
        KafkaClusterConfig cluster,
        string groupId,
        IReadOnlyList<TopicPartition> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets,
        CancellationToken cancellationToken)
    {
        using var consumer = CreateOffsetQueryConsumer(cluster, groupId);
        var accumulator = new ConsumerOffsetAccumulator();
        var batchSize = Math.Max(1, Option.OffsetQueryBatchSize);

        foreach (var batch in partitions.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsumerOffsetTotals total;
            try
            {
                var offsets = consumer.Committed(batch, Option.AdminRequestTimeout);
                total = CalculateConsumerOffsetTotals(offsets, latestOffsets);
            }
            catch (TopicPartitionOffsetException ex)
            {
                var validOffsets = ex.Results
                    .Where(result => result.Error.Code == ErrorCode.NoError)
                    .Select(result => result.TopicPartitionOffset);
                total = CalculateConsumerOffsetTotals(validOffsets, latestOffsets);
            }

            accumulator.Add(total);

            // A blocking native query cannot be abandoned safely. Observe cancellation only after
            // it has returned so the consumer is disposed after all native work has completed.
            cancellationToken.ThrowIfCancellationRequested();
        }

        return accumulator.Build();
    }

    private static ConsumerOffsetTotals CalculateConsumerOffsetTotals(
        IEnumerable<TopicPartitionOffset> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets)
    {
        var byTopic = new Dictionary<string, TopicConsumerOffsetTotals>(StringComparer.Ordinal);

        foreach (var partition in partitions)
        {
            if (partition.Offset.Value < 0 ||
                !latestOffsets.ContainsKey(partition.TopicPartition))
            {
                continue;
            }

            var topicName = partition.TopicPartition.Topic;
            var current = byTopic.GetValueOrDefault(topicName);
            byTopic[topicName] = new TopicConsumerOffsetTotals(
                current.TotalCommittedOffset + partition.Offset.Value,
                current.TotalLag + CalculateLag(partition, latestOffsets));
        }

        if (byTopic.Count == 0)
        {
            return ConsumerOffsetTotals.Empty;
        }

        return new ConsumerOffsetTotals(
            byTopic.Values.Sum(offsets => offsets.TotalCommittedOffset),
            byTopic.Values.Sum(offsets => offsets.TotalLag),
            byTopic);
    }

    private static long CalculateLag(
        TopicPartitionOffset partition,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets)
    {
        return latestOffsets.TryGetValue(partition.TopicPartition, out var latestOffset)
            ? Math.Max(0, latestOffset - partition.Offset.Value)
            : 0;
    }

    private readonly record struct TopicConsumerOffsetTotals(long TotalCommittedOffset, long TotalLag);

    private sealed class ConsumerOffsetAccumulator
    {
        private readonly Dictionary<string, TopicConsumerOffsetTotals> _byTopic = new(StringComparer.Ordinal);
        private long _committedOffset;
        private long _lag;
        private bool _hasOffsets;

        public void Add(ConsumerOffsetTotals offsets)
        {
            if (!offsets.TotalCommittedOffset.HasValue)
            {
                return;
            }

            _hasOffsets = true;
            _committedOffset += offsets.TotalCommittedOffset.Value;
            _lag += offsets.TotalLag.GetValueOrDefault();

            foreach (var (topicName, topicOffsets) in offsets.ByTopic)
            {
                var current = _byTopic.GetValueOrDefault(topicName);
                _byTopic[topicName] = new TopicConsumerOffsetTotals(
                    current.TotalCommittedOffset + topicOffsets.TotalCommittedOffset,
                    current.TotalLag + topicOffsets.TotalLag);
            }
        }

        public ConsumerOffsetTotals Build()
        {
            return _hasOffsets
                ? new ConsumerOffsetTotals(_committedOffset, _lag, _byTopic)
                : ConsumerOffsetTotals.Empty;
        }
    }

    private sealed record ConsumerOffsetTotals(
        long? TotalCommittedOffset,
        long? TotalLag,
        IReadOnlyDictionary<string, TopicConsumerOffsetTotals> ByTopic)
    {
        public static ConsumerOffsetTotals Empty { get; } = new(
            null,
            null,
            new Dictionary<string, TopicConsumerOffsetTotals>(StringComparer.Ordinal));
    }

    private sealed record PartitionWatermark(long EarliestOffset, long LatestOffset)
    {
        public long AvailableMessageCount => Math.Max(0, LatestOffset - EarliestOffset);
    }
}
