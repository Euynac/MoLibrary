using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services.Support;

/// <summary>
/// Calculates interval rates from two Kafka offset snapshots.
/// </summary>
internal static class KafkaPerformanceRateCalculator
{
    /// <summary>
    /// Applies write and consume rates to the current snapshot when both offset baselines are valid.
    /// </summary>
    /// <param name="current">Current snapshot to update.</param>
    /// <param name="previous">Immediately preceding comparable snapshot.</param>
    public static void ApplyRates(KafkaPerformanceSnapshot current, KafkaPerformanceSnapshot? previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null)
        {
            return;
        }

        var elapsedSeconds = (current.CapturedAt - previous.CapturedAt).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return;
        }

        current.MessageWriteRatePerSecond = CalculateRate(
            previous.TotalLogEndOffset,
            current.TotalLogEndOffset,
            elapsedSeconds);
        current.MessageConsumeRatePerSecond = CalculateRate(
            previous.TotalConsumerCommittedOffset,
            current.TotalConsumerCommittedOffset,
            elapsedSeconds);

        ApplyTopicRates(current, previous, elapsedSeconds);
        ApplyConsumerGroupRates(current.ConsumerGroupMetrics, previous.ConsumerGroupMetrics, elapsedSeconds);
    }

    /// <summary>
    /// Applies interval rates to a standalone detailed consumer metrics capture.
    /// </summary>
    /// <param name="current">Current capture.</param>
    /// <param name="previous">Previous comparable capture.</param>
    public static void ApplyRates(KafkaConsumerMetricsSnapshot current, KafkaConsumerMetricsSnapshot? previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null)
        {
            return;
        }

        var elapsedSeconds = (current.CapturedAt - previous.CapturedAt).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return;
        }

        ApplyConsumerGroupRates(current.GroupMetrics, previous.GroupMetrics, elapsedSeconds);
    }

    private static void ApplyTopicRates(
        KafkaPerformanceSnapshot current,
        KafkaPerformanceSnapshot previous,
        double elapsedSeconds)
    {
        if (current.TopicMetrics.Count == 0 || previous.TopicMetrics.Count == 0)
        {
            return;
        }

        var previousByTopic = previous.TopicMetrics
            .Where(metric => !string.IsNullOrWhiteSpace(metric.TopicName))
            .GroupBy(metric => metric.TopicName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        foreach (var metric in current.TopicMetrics)
        {
            if (!previousByTopic.TryGetValue(metric.TopicName, out var previousMetric))
            {
                continue;
            }

            metric.MessageWriteRatePerSecond = CalculateRate(
                previousMetric.TotalLogEndOffset,
                metric.TotalLogEndOffset,
                elapsedSeconds);
            metric.MessageConsumeRatePerSecond = CalculateRate(
                previousMetric.TotalConsumerCommittedOffset,
                metric.TotalConsumerCommittedOffset,
                elapsedSeconds);
        }
    }

    private static void ApplyConsumerGroupRates(
        IReadOnlyList<KafkaConsumerGroupTopicMetrics> current,
        IReadOnlyList<KafkaConsumerGroupTopicMetrics> previous,
        double elapsedSeconds)
    {
        if (current.Count == 0 || previous.Count == 0)
        {
            return;
        }

        var previousByTopicAndGroup = previous
            .Where(metric => !string.IsNullOrWhiteSpace(metric.TopicName) &&
                             !string.IsNullOrWhiteSpace(metric.GroupId))
            .GroupBy(metric => (metric.TopicName, metric.GroupId))
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (var metric in current)
        {
            if (!previousByTopicAndGroup.TryGetValue((metric.TopicName, metric.GroupId), out var previousMetric))
            {
                continue;
            }

            metric.WriteRatePerSecond = CalculateRate(
                previousMetric.TotalLogEndOffset,
                metric.TotalLogEndOffset,
                elapsedSeconds);
            metric.ConsumeRatePerSecond = CalculateRate(
                previousMetric.TotalCurrentOffset,
                metric.TotalCurrentOffset,
                elapsedSeconds);

            var previousPartitions = previousMetric.Members
                .SelectMany(member => member.Partitions)
                .GroupBy(partition => partition.Partition)
                .ToDictionary(group => group.Key, group => group.Last());
            foreach (var partition in metric.Members.SelectMany(member => member.Partitions))
            {
                if (!previousPartitions.TryGetValue(partition.Partition, out var previousPartition))
                {
                    continue;
                }

                partition.WriteRatePerSecond = CalculateRate(
                    previousPartition.LogEndOffset,
                    partition.LogEndOffset,
                    elapsedSeconds);
                partition.ConsumeRatePerSecond = CalculateRate(
                    previousPartition.CurrentOffset,
                    partition.CurrentOffset,
                    elapsedSeconds);
            }
        }
    }

    private static double? CalculateRate(long? previous, long? current, double elapsedSeconds)
    {
        if (!previous.HasValue || !current.HasValue || current.Value < previous.Value)
        {
            return null;
        }

        return (current.Value - previous.Value) / elapsedSeconds;
    }
}
