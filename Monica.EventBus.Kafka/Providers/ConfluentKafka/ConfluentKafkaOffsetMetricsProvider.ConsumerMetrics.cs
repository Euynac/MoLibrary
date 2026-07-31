using Confluent.Kafka;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

internal sealed partial class ConfluentKafkaOffsetMetricsProvider
{
    /// <summary>
    /// Captures member assignments and partition-level offsets for the supplied consumer groups.
    /// </summary>
    public async Task<KafkaConsumerMetricsSnapshot> CaptureConsumerMetricsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupDescription> consumerGroups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(consumerGroups);
        cancellationToken.ThrowIfCancellationRequested();

        var applicationTopics = topics
            .Where(topic => !topic.IsInternal)
            .OrderBy(topic => topic.TopicName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sampleableTopics = applicationTopics
            .Where(topic => topic.IsMetadataAvailable && topic.Partitions > 0)
            .ToList();
        var allPartitions = BuildTopicPartitions(applicationTopics);
        var capturedAt = DateTimeOffset.UtcNow;
        Dictionary<TopicPartition, PartitionWatermark> watermarks;
        if (allPartitions.Count == 0)
        {
            watermarks = [];
        }
        else
        {
            using var admin = CreateAdminClient(cluster);
            watermarks = await ReadWatermarksAsync(admin, allPartitions, cancellationToken);
        }

        var metrics = new List<KafkaConsumerGroupTopicMetrics>();
        var groups = consumerGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId))
            .Take(Math.Max(0, Option.MaxConsumerGroupsToInspect))
            .ToList();
        IReadOnlyList<GroupOffsetCapture> groupOffsetCaptures;
        if (groups.Count == 0 || allPartitions.Count == 0)
        {
            groupOffsetCaptures = [];
        }
        else
        {
            var parallelism = Math.Clamp(Option.ConsumerOffsetQueryParallelism, 1, groups.Count);
            using var gate = new SemaphoreSlim(parallelism, parallelism);
            var groupTasks = groups.Select(async group =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var offsets = await KafkaNativeRequestAwaiter.RunBlockingAsync(
                        () => ReadConsumerGroupPartitionOffsets(
                            cluster,
                            group.GroupId.Trim(),
                            allPartitions,
                            cancellationToken),
                        cancellationToken);
                    return new GroupOffsetCapture(offsets, null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    group.ErrorMessage = string.IsNullOrWhiteSpace(group.ErrorMessage)
                        ? ex.Message
                        : $"{group.ErrorMessage}; {ex.Message}";
                    return new GroupOffsetCapture(
                        new Dictionary<TopicPartition, long?>(),
                        ex.Message);
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();
            groupOffsetCaptures = await Task.WhenAll(groupTasks);
        }

        var offsetsByGroup = groupOffsetCaptures
            .Select(capture => capture.Offsets)
            .ToList();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = groups[groupIndex];
            var offsets = offsetsByGroup.Count > groupIndex
                ? offsetsByGroup[groupIndex]
                : new Dictionary<TopicPartition, long?>();
            foreach (var topic in sampleableTopics)
            {
                var topicPartitions = Enumerable.Range(0, topic.Partitions)
                    .Select(partition => new TopicPartition(topic.TopicName, new Partition(partition)))
                    .ToList();
                var hasAssignment = group.Members.Any(member => member.Partitions.Any(assignment =>
                    string.Equals(assignment.TopicName, topic.TopicName, StringComparison.Ordinal)));
                var hasCommittedOffset = topicPartitions.Any(partition => offsets.GetValueOrDefault(partition).HasValue);
                if (!hasAssignment && !hasCommittedOffset)
                {
                    continue;
                }

                var partitionMetrics = topicPartitions
                    .Select(partition => CreatePartitionMetrics(partition, offsets, watermarks))
                    .ToList();
                var members = BuildMemberMetrics(group, topic.TopicName, partitionMetrics);

                metrics.Add(new KafkaConsumerGroupTopicMetrics
                {
                    TopicName = topic.TopicName,
                    GroupId = group.GroupId,
                    State = group.State,
                    CapturedAt = capturedAt,
                    TotalCurrentOffset = SumOffsets(partitionMetrics.Select(partition => partition.CurrentOffset)),
                    TotalLogEndOffset = SumOffsets(partitionMetrics.Select(partition => partition.LogEndOffset)),
                    TotalRetainedMessageCount = SumOffsets(topicPartitions.Select(partition =>
                        watermarks.TryGetValue(partition, out var watermark)
                            ? watermark.RetainedMessageCount
                            : (long?)null)),
                    TotalLag = SumOffsets(partitionMetrics.Select(partition => partition.Lag)),
                    Members = members
                });
            }
        }

        var offsetDiagnostics = groupOffsetCaptures
            .Select((capture, index) => new { Capture = capture, Group = groups[index] })
            .Where(item => !string.IsNullOrWhiteSpace(item.Capture.ErrorMessage))
            .Select(item => $"{item.Group.GroupId}: {item.Capture.ErrorMessage}")
            .ToList();
        return new KafkaConsumerMetricsSnapshot
        {
            ClusterId = cluster.ClusterId,
            CapturedAt = capturedAt,
            Message = offsetDiagnostics.Count == 0 ? null : string.Join("; ", offsetDiagnostics),
            ConsumerGroups = groups,
            GroupMetrics = metrics
                .OrderBy(metric => metric.TopicName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(metric => metric.GroupId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PerformanceOffsetTotals = CreatePerformanceOffsetTotals(
                applicationTopics,
                allPartitions,
                watermarks,
                AggregateConsumerOffsets(offsetsByGroup, watermarks),
                groupOffsetCaptures.All(capture => string.IsNullOrWhiteSpace(capture.ErrorMessage)))
        };
    }

    private static ConsumerOffsetTotals AggregateConsumerOffsets(
        IReadOnlyList<Dictionary<TopicPartition, long?>> offsetsByGroup,
        IReadOnlyDictionary<TopicPartition, PartitionWatermark> watermarks)
    {
        if (offsetsByGroup.Count == 0 || watermarks.Count == 0)
        {
            return ConsumerOffsetTotals.Empty;
        }

        var latestOffsets = watermarks.ToDictionary(
            item => item.Key,
            item => item.Value.LatestOffset);
        var accumulator = new ConsumerOffsetAccumulator();
        foreach (var groupOffsets in offsetsByGroup)
        {
            var committedOffsets = groupOffsets
                .Where(item => item.Value.HasValue)
                .Select(item => new TopicPartitionOffset(item.Key, new Offset(item.Value!.Value)));
            accumulator.Add(CalculateConsumerOffsetTotals(committedOffsets, latestOffsets));
        }

        return accumulator.Build();
    }

    private Dictionary<TopicPartition, long?> ReadConsumerGroupPartitionOffsets(
        KafkaClusterConfig cluster,
        string groupId,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        using var consumer = CreateOffsetQueryConsumer(cluster, groupId);
        var offsets = partitions.ToDictionary(partition => partition, _ => (long?)null);
        var batchSize = Math.Max(1, Option.OffsetQueryBatchSize);
        foreach (var batch in partitions.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var item in consumer.Committed(batch, Option.AdminRequestTimeout))
                {
                    offsets[item.TopicPartition] = item.Offset.Value >= 0 ? item.Offset.Value : null;
                }
            }
            catch (TopicPartitionOffsetException ex)
            {
                foreach (var item in ex.Results.Where(result => result.Error.Code == ErrorCode.NoError))
                {
                    offsets[item.TopicPartitionOffset.TopicPartition] =
                        item.TopicPartitionOffset.Offset.Value >= 0 ? item.TopicPartitionOffset.Offset.Value : null;
                }
            }

            // Do not dispose a native consumer while a blocking query is still running.
            cancellationToken.ThrowIfCancellationRequested();
        }

        return offsets;
    }

    private static KafkaConsumerPartitionMetrics CreatePartitionMetrics(
        TopicPartition partition,
        IReadOnlyDictionary<TopicPartition, long?> offsets,
        IReadOnlyDictionary<TopicPartition, PartitionWatermark> watermarks)
    {
        var currentOffset = offsets.GetValueOrDefault(partition);
        var logEndOffset = watermarks.TryGetValue(partition, out var watermark)
            ? watermark.LatestOffset
            : (long?)null;
        return new KafkaConsumerPartitionMetrics
        {
            Partition = partition.Partition.Value,
            CurrentOffset = currentOffset,
            LogEndOffset = logEndOffset,
            Lag = currentOffset.HasValue && logEndOffset.HasValue
                ? Math.Max(0, logEndOffset.Value - currentOffset.Value)
                : null
        };
    }

    private static IReadOnlyList<KafkaConsumerMemberMetrics> BuildMemberMetrics(
        KafkaConsumerGroupDescription group,
        string topicName,
        IReadOnlyList<KafkaConsumerPartitionMetrics> partitionMetrics)
    {
        var byPartition = partitionMetrics.ToDictionary(partition => partition.Partition);
        var memberBuckets = group.Members
            .Select(member => new KafkaConsumerMemberMetrics
            {
                ConsumerId = member.ConsumerId,
                Host = member.Host,
                ClientId = member.ClientId,
                Partitions = member.Partitions
                    .Where(assignment => string.Equals(assignment.TopicName, topicName, StringComparison.Ordinal) &&
                                         byPartition.ContainsKey(assignment.Partition))
                    .Select(assignment => byPartition[assignment.Partition])
                    .OrderBy(partition => partition.Partition)
                    .ToList()
            })
            .Where(member => member.Partitions.Count > 0)
            .ToList();

        var assignedPartitions = memberBuckets
            .SelectMany(member => member.Partitions)
            .Select(partition => partition.Partition)
            .ToHashSet();
        var unassigned = partitionMetrics
            .Where(partition => !assignedPartitions.Contains(partition.Partition))
            .OrderBy(partition => partition.Partition)
            .ToList();
        if (unassigned.Count > 0)
        {
            memberBuckets.Add(new KafkaConsumerMemberMetrics
            {
                ConsumerId = string.Empty,
                Host = string.Empty,
                ClientId = string.Empty,
                Partitions = unassigned
            });
        }

        return memberBuckets
            .OrderBy(member => member.ConsumerId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static long? SumOffsets(IEnumerable<long?> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 || materialized.Any(value => !value.HasValue)
            ? null
            : materialized.Sum(value => value!.Value);
    }

    private sealed record GroupOffsetCapture(
        Dictionary<TopicPartition, long?> Offsets,
        string? ErrorMessage);
}

