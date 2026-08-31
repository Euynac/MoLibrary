using Monica.EventBus.Models;

namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// A real-time change pushed by <see cref="State.EventBusMonitorService"/> to open monitor pages.
/// Either a subscription registry change or a topic runtime-status change; both kinds trigger
/// a data refresh, but only subscription changes feed the recent-changes list.
/// </summary>
public abstract record EventBusMonitorChange
{
    /// <summary>
    /// A subscription was added, activated, deactivated, or removed.
    /// </summary>
    public sealed record SubscriptionChanged(SubscriptionChangeViewModel Change) : EventBusMonitorChange;

    /// <summary>
    /// The runtime health of a topic subscription changed (state transition or reported error).
    /// </summary>
    public sealed record TopicStatusChanged(TopicSubscriptionStatus Status) : EventBusMonitorChange;
}
