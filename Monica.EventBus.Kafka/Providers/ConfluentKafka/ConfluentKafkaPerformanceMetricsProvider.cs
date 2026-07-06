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
internal sealed class ConfluentKafkaPerformanceMetricsProvider(IOptions<ModuleEventBusKafkaOption> options)
    : IKafkaPerformanceMetricsProvider
{
    private ModuleEventBusKafkaOption Option => options.Value;

    public async Task<KafkaPerformanceOffsetTotals> CaptureOffsetTotalsAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken = default)
    {
        var partitions = BuildTopicPartitions(topics);
        if (partitions.Count == 0)
        {
            return new KafkaPerformanceOffsetTotals();
        }

        using var admin = new AdminClientBuilder(KafkaClientConfigFactory.BuildAdminConfig(cluster, Option)).Build();
        var latestOffsets = await ReadLatestOffsetsAsync(admin, partitions);
        var consumerOffsets = await ReadConsumerOffsetsAsync(admin, partitions, latestOffsets, consumerGroups, cancellationToken);

        return new KafkaPerformanceOffsetTotals
        {
            TotalLogEndOffset = latestOffsets.Values.Sum(),
            TotalConsumerCommittedOffset = consumerOffsets.TotalCommittedOffset,
            TotalLag = consumerOffsets.TotalLag
        };
    }

    private static IReadOnlyList<TopicPartition> BuildTopicPartitions(IReadOnlyList<KafkaTopicSummary> topics)
    {
        return topics
            .Where(topic => !topic.IsInternal && topic.Partitions > 0)
            .SelectMany(topic => Enumerable.Range(0, topic.Partitions)
                .Select(partition => new TopicPartition(topic.TopicName, new Partition(partition))))
            .ToList();
    }

    private async Task<Dictionary<TopicPartition, long>> ReadLatestOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions)
    {
        var specs = partitions.Select(partition => new TopicPartitionOffsetSpec
        {
            TopicPartition = partition,
            OffsetSpec = OffsetSpec.Latest()
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

    private async Task<ConsumerOffsetTotals> ReadConsumerOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        IReadOnlyDictionary<TopicPartition, long> latestOffsets,
        IReadOnlyList<KafkaConsumerGroupSummary> consumerGroups,
        CancellationToken cancellationToken)
    {
        long committedTotal = 0;
        long lagTotal = 0;
        var hasConsumerOffsets = false;

        foreach (var group in consumerGroups.Where(group => !string.IsNullOrWhiteSpace(group.GroupId)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var results = await admin.ListConsumerGroupOffsetsAsync(
                    [new ConsumerGroupTopicPartitions(group.GroupId, partitions.ToList())],
                    new ListConsumerGroupOffsetsOptions
                    {
                        RequestTimeout = Option.AdminRequestTimeout,
                        RequireStableOffsets = false
                    });

                foreach (var partition in results.SelectMany(result => result.Partitions))
                {
                    if (partition.Error.Code != ErrorCode.NoError || partition.Offset.Value < 0)
                    {
                        continue;
                    }

                    hasConsumerOffsets = true;
                    committedTotal += partition.Offset.Value;
                    lagTotal += CalculateLag(partition, latestOffsets);
                }
            }
            catch (ListConsumerGroupOffsetsException ex)
            {
                foreach (var partition in ex.Results.SelectMany(result => result.Partitions))
                {
                    if (partition.Error.Code != ErrorCode.NoError || partition.Offset.Value < 0)
                    {
                        continue;
                    }

                    hasConsumerOffsets = true;
                    committedTotal += partition.Offset.Value;
                    lagTotal += CalculateLag(partition, latestOffsets);
                }
            }
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
}
