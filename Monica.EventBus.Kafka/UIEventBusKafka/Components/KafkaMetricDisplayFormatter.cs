using Microsoft.Extensions.Localization;
using Monica.EventBus.Kafka.Localization;

namespace Monica.EventBus.Kafka.UIEventBusKafka.Components;

/// <summary>
/// Provides consistent localized formatting for optional Kafka console metrics.
/// </summary>
internal static class KafkaMetricDisplayFormatter
{
    /// <summary>
    /// Formats a nullable integer metric while preserving the caller's resource-specific unknown label.
    /// </summary>
    public static string FormatOptionalInt64(long? value, string unknownValue)
    {
        return value?.ToString() ?? unknownValue;
    }

    /// <summary>
    /// Formats a nullable per-second rate with the shared Kafka performance resource contract.
    /// </summary>
    public static string FormatRate(
        double? rate,
        IStringLocalizer<EventBusKafkaResource> localizer)
    {
        return rate.HasValue
            ? localizer["Performance:Values:RatePerSecond", rate.Value.ToString("0.##")]
            : localizer["Performance:Values:RateUnknown"];
    }
}
