namespace Monica.EventBus.Models;

/// <summary>
/// Subscription lifecycle states with clear state machine transitions.
/// </summary>
public enum SubscriptionState
{
    /// <summary>
    /// Subscription created but not yet active (initial state).
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Subscription is active and receiving events.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Subscription is temporarily deactivated but can be reactivated.
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Subscription has been disposed and cannot be reused.
    /// </summary>
    Disposed = 3
}
