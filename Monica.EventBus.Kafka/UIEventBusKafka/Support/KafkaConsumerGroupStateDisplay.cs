using Microsoft.Extensions.Localization;
using Monica.EventBus.Kafka.Localization;
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

    /// <summary>
    /// Gets the localized display text for the supplied consumer group state.
    /// </summary>
    /// <param name="state">The Kafka consumer group lifecycle state.</param>
    /// <param name="localizer">The Kafka console localization source.</param>
    /// <returns>A localized lifecycle label, or the provider value when the state is unknown.</returns>
    public static string GetText(
        string state,
        IStringLocalizer<EventBusKafkaResource> localizer) => state.ToUpperInvariant() switch
    {
        "STABLE" => localizer["ConsumerGroups:States:Stable"],
        "PREPARINGREBALANCE" => localizer["ConsumerGroups:States:PreparingRebalance"],
        "COMPLETINGREBALANCE" => localizer["ConsumerGroups:States:CompletingRebalance"],
        "DEAD" => localizer["ConsumerGroups:States:Dead"],
        "EMPTY" => localizer["ConsumerGroups:States:Empty"],
        _ => string.IsNullOrWhiteSpace(state)
            ? localizer["ConsumerGroups:States:Unknown"]
            : state
    };
}
