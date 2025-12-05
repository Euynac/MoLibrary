using MoLibrary.EventBus.Abstractions.Subscriptions;

namespace MoLibrary.EventBus.Models;

/// <summary>
/// Represents a subscription change notification.
/// </summary>
public sealed record SubscriptionChange(
    SubscriptionChangeType ChangeType,
    ISubscription Subscription,
    DateTimeOffset Timestamp);
