using Monica.EventBus.Abstractions;

namespace Monica.EventBus.Models;

/// <summary>
/// Represents a subscription change notification.
/// </summary>
public sealed record EventSubscriptionChange(
    EventSubscriptionChangeType ChangeType,
    IEventSubscription Subscription,
    DateTime Timestamp);
