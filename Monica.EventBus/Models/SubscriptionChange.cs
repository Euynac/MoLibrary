using Monica.EventBus.Abstractions.Subscriptions;

namespace Monica.EventBus.Models;

/// <summary>
/// Represents a subscription change notification.
/// </summary>
public sealed record SubscriptionChange(
    SubscriptionChangeType ChangeType,
    ISubscription Subscription,
    DateTime Timestamp);
