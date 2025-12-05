namespace MoLibrary.EventBus.Models;

/// <summary>
/// Subscription scope - local vs distributed.
/// </summary>
public enum SubscriptionScope
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
