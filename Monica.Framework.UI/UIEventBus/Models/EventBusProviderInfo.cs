using Monica.EventBus.Providers;

namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// Represents registered EventBus Provider information
/// </summary>
public class EventBusProviderInfo
{
    /// <summary>
    /// Service key (null indicates a non-Keyed default Provider)
    /// </summary>
    public string? ServiceKey { get; init; }

    /// <summary>
    /// display name
    /// </summary>
    public string DisplayName => ServiceKey ?? "默认";

    /// <summary>
    /// Provider type
    /// </summary>
    public EEventBusProviderType ProviderType { get; init; }

    /// <summary>
    /// Provider capabilities
    /// </summary>
    public EEventBusCapabilities Capabilities { get; init; }

    /// <summary>
    /// Whether it is distributed EventBus
    /// </summary>
    public bool IsDistributed { get; init; }

    /// <summary>
    /// Type of Provider Option
    /// </summary>
    public Type? OptionType { get; init; }

    /// <summary>
    /// Example of Provider Option
    /// </summary>
    public object? OptionInstance { get; init; }

    /// <summary>
    /// Implementation type name
    /// </summary>
    public string ImplementationType { get; init; } = "";

    /// <summary>
    /// Total number of subscriptions
    /// </summary>
    public int SubscriptionCount { get; set; }

    /// <summary>
    /// Number of active subscriptions
    /// </summary>
    public int ActiveSubscriptionCount { get; set; }

    /// <summary>
    /// Check if the Provider supports batch publishing
    /// </summary>
    public bool SupportsBulkPublish => Capabilities.HasFlag(EEventBusCapabilities.BulkPublish);

    /// <summary>
    /// Check if the Provider supports streaming subscriptions
    /// </summary>
    public bool SupportsStreaming => Capabilities.HasFlag(EEventBusCapabilities.Streaming);

    /// <summary>
    /// Check if the Provider supports dead letter queues
    /// </summary>
    public bool SupportsDeadLetterQueue => Capabilities.HasFlag(EEventBusCapabilities.DeadLetterQueue);

    /// <summary>
    /// Get the display name of the Provider type
    /// </summary>
    public string ProviderTypeName => ProviderType switch
    {
        EEventBusProviderType.Local => "本地",
        EEventBusProviderType.Dapr => "Dapr",
        _ => "未知"
    };

    /// <summary>
    /// Get unique identifier (for comparison and selection)
    /// </summary>
    public string UniqueId => $"{ProviderType}:{ServiceKey ?? "default"}:{(IsDistributed ? "dist" : "local")}";
}
