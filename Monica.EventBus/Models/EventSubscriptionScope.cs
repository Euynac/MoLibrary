namespace Monica.EventBus.Models;

/// <summary>
/// EventSubscription scope - local vs distributed.
/// </summary>
public enum EventSubscriptionScope
{
    /// <summary>
    /// In-process, local event handling.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Cross-process, distributed event handling.
    /// </summary>
    Distributed = 1
}
