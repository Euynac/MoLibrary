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
