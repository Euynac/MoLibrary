namespace MoLibrary.EventBus.Models;

/// <summary>
/// Change event types for subscription observations.
/// </summary>
public enum SubscriptionChangeType
{
    /// <summary>
    /// Subscription was added to the subscription manager.
    /// </summary>
    Added,

    /// <summary>
    /// Subscription was activated (Pending → Active or Inactive → Active).
    /// </summary>
    Activated,

    /// <summary>
    /// Subscription was deactivated (Active → Inactive).
    /// </summary>
    Deactivated,

    /// <summary>
    /// Subscription was removed/disposed from the subscription manager.
    /// </summary>
    Removed
}
