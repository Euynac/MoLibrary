using Monica.EventBus.Models;

namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// Subscription filters
/// </summary>
public class SubscriptionFilter
{
    /// <summary>
    /// Filter by status
    /// </summary>
    public EventSubscriptionState? State { get; set; }

    /// <summary>
    /// Filter by the runtime state of the underlying topic subscription
    /// (recovering, failed, and so on). Only distributed subscriptions with a reported
    /// runtime state can match.
    /// </summary>
    public TopicSubscriptionRuntimeState? TopicRuntimeState { get; set; }

    /// <summary>
    /// Filter by range
    /// </summary>
    public EventSubscriptionScope? Scope { get; set; }

    /// <summary>
    /// Filter by service key
    /// </summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// Filter by whether to automatically discover
    /// </summary>
    public bool? IsAutoDiscovered { get; set; }

    /// <summary>
    /// Search text (search within event type or topic name)
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Selected Provider (for filtering by Provider)
    /// </summary>
    public EventBusProviderInfo? SelectedProvider { get; set; }

    /// <summary>
    /// Are there any filters?
    /// </summary>
    public bool HasAnyFilter =>
        State.HasValue ||
        TopicRuntimeState.HasValue ||
        Scope.HasValue ||
        !string.IsNullOrWhiteSpace(ServiceKey) ||
        IsAutoDiscovered.HasValue ||
        !string.IsNullOrWhiteSpace(SearchText) ||
        SelectedProvider != null;

    /// <summary>
    /// Clear all filters
    /// </summary>
    public void Clear()
    {
        State = null;
        TopicRuntimeState = null;
        Scope = null;
        ServiceKey = null;
        IsAutoDiscovered = null;
        SearchText = null;
        SelectedProvider = null;
    }

    /// <summary>
    /// Create a copy
    /// </summary>
    public SubscriptionFilter Clone()
    {
        return new SubscriptionFilter
        {
            State = State,
            TopicRuntimeState = TopicRuntimeState,
            Scope = Scope,
            ServiceKey = ServiceKey,
            IsAutoDiscovered = IsAutoDiscovered,
            SearchText = SearchText,
            SelectedProvider = SelectedProvider
        };
    }
}
