using Monica.Core.Localization.Services;
using Monica.EventBus.Abstractions;
using Monica.Framework.UI.Localization;

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
    public string DisplayName => ServiceKey ?? LocalizationManager.Get<EventBusResource>("Services:Common:DefaultProviderName");

    /// <summary>
    /// Provider type
    /// </summary>
    public EventBusProviderKind ProviderType { get; init; }

    /// <summary>
    /// Provider capabilities
    /// </summary>
    public EventBusProviderCapabilities Capabilities { get; init; }

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
    public bool SupportsBulkPublish => Capabilities.HasFlag(EventBusProviderCapabilities.BulkPublish);

    /// <summary>
    /// Check if the Provider supports streaming subscriptions
    /// </summary>
    public bool SupportsStreaming => Capabilities.HasFlag(EventBusProviderCapabilities.Streaming);

    /// <summary>
    /// Check if the Provider supports dead letter queues
    /// </summary>
    public bool SupportsDeadLetterQueue => Capabilities.HasFlag(EventBusProviderCapabilities.DeadLetterQueue);

    /// <summary>
    /// Get the display name of the Provider type
    /// </summary>
    public string ProviderTypeName => LocalizationManager.For<EventBusResource>().GetProviderKindText(ProviderType);

    /// <summary>
    /// Get unique identifier (for comparison and selection)
    /// </summary>
    public string UniqueId => $"{ProviderType}:{ServiceKey ?? "default"}:{(IsDistributed ? "dist" : "local")}";
}
