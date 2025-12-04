using MoLibrary.EventBus.Models;

namespace MoLibrary.EventBus.Abstractions.Subscriptions;

/// <summary>
/// Fluent builder for creating complex subscriptions.
/// </summary>
public interface ISubscriptionBuilder
{
    /// <summary>
    /// Specifies the event type for the subscription.
    /// </summary>
    ISubscriptionBuilder ForEvent<TEvent>() where TEvent : class;

    /// <summary>
    /// Specifies the event type for the subscription.
    /// </summary>
    ISubscriptionBuilder ForEvent(Type eventType);

    /// <summary>
    /// Specifies a custom topic name (overrides EventNameAttribute).
    /// </summary>
    ISubscriptionBuilder WithTopic(string topicName);

    /// <summary>
    /// Specifies the handler type.
    /// </summary>
    ISubscriptionBuilder WithHandler<THandler>() where THandler : IMoEventHandler;

    /// <summary>
    /// Specifies the handler type.
    /// </summary>
    ISubscriptionBuilder WithHandler(Type handlerType);

    /// <summary>
    /// Specifies the handler action.
    /// </summary>
    ISubscriptionBuilder WithHandler<TEvent>(Func<TEvent, Task> action) where TEvent : class;

    /// <summary>
    /// Specifies a custom handler factory.
    /// </summary>
    ISubscriptionBuilder WithHandlerFactory(IEventHandlerFactory factory);

    /// <summary>
    /// Specifies the subscription scope (Local/Distributed).
    /// </summary>
    ISubscriptionBuilder WithScope(SubscriptionScope scope);

    /// <summary>
    /// Adds custom metadata to the subscription.
    /// </summary>
    ISubscriptionBuilder WithMetadata(string key, object value);

    /// <summary>
    /// Adds multiple metadata entries.
    /// </summary>
    ISubscriptionBuilder WithMetadata(IReadOnlyDictionary<string, object> metadata);

    /// <summary>
    /// Marks this subscription as auto-discovered.
    /// </summary>
    ISubscriptionBuilder AsAutoDiscovered();

    /// <summary>
    /// Creates and activates the subscription.
    /// </summary>
    Task<ISubscription> SubscribeAsync();

    /// <summary>
    /// Builds the subscription descriptor without subscribing.
    /// </summary>
    SubscriptionDescriptor Build();
}
