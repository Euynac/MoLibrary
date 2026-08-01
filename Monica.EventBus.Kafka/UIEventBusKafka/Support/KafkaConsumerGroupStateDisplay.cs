using MudBlazor;

namespace Monica.EventBus.Kafka.UIEventBusKafka.Support;

/// <summary>
/// Resolves Kafka consumer group lifecycle states to semantic UI colors.
/// </summary>
internal static class KafkaConsumerGroupStateDisplay
{
    /// <summary>
    /// Gets the MudBlazor color that represents the supplied consumer group state.
    /// </summary>
    /// <param name="state">The Kafka consumer group lifecycle state.</param>
    /// <returns>The semantic color used wherever the state is displayed.</returns>
    public static Color GetColor(string state) => state.ToUpperInvariant() switch
    {
        "STABLE" => Color.Success,
        "PREPARINGREBALANCE" or "COMPLETINGREBALANCE" => Color.Warning,
        "DEAD" => Color.Error,
        "EMPTY" => Color.Secondary,
        _ => Color.Info
    };
}
