namespace Monica.EventBus.Models;

/// <summary>
/// Change event types for subscription observations.
/// </summary>
public enum EventSubscriptionChangeType
{
    /// <summary>
    /// EventSubscription was added to the subscription manager.
    /// </summary>
    Added,

    /// <summary>
    /// EventSubscription was activated (Pending → Active or Inactive → Active).
    /// </summary>
    Activated,

    /// <summary>
    /// EventSubscription was deactivated (Active → Inactive).
    /// </summary>
    Deactivated,

    /// <summary>
    /// EventSubscription was removed/disposed from the subscription manager.
    /// </summary>
    Removed
}
