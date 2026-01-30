using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Models;

/// <summary>
/// Descriptor for creating a new subscription.
/// Contains all necessary information to create and activate a subscription.
/// </summary>
public sealed record SubscriptionDescriptor
{
    /// <summary>
    /// Service key for keyed EventBus instances. Null for default EventBus.
    /// </summary>
    public string? ServiceKey { get; init; }

    /// <summary>
    /// Event type to subscribe to.
    /// </summary>
    public required Type EventType { get; init; }

    /// <summary>
    /// Topic name for routing the event (can be customized).
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// Handler factory for creating handler instances.
    /// </summary>
    public required IEventHandlerFactory HandlerFactory { get; init; }

    /// <summary>
    /// Subscription scope (Local or Distributed).
    /// </summary>
    public required SubscriptionScope Scope { get; init; }

    /// <summary>
    /// Whether this subscription was auto-discovered during startup.
    /// </summary>
    public bool IsAutoDiscovered { get; init; }

    /// <summary>
    /// Custom metadata associated with the subscription.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
