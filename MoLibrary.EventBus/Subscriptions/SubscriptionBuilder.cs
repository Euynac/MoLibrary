using Microsoft.Extensions.DependencyInjection;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.EventBus.Models;

namespace MoLibrary.EventBus.Subscriptions;

/// <summary>
/// Fluent builder for creating complex subscriptions.
/// </summary>
public class SubscriptionBuilder : ISubscriptionBuilder
{
    private readonly ISubscriptionManager _subscriptionManager;
    private Type? _eventType;
    private string? _topicName;
    private IEventHandlerFactory? _handlerFactory;
    private Type? _handlerType;
    private SubscriptionScope? _scope;
    private bool _isAutoDiscovered;
    private readonly Dictionary<string, object> _metadata = new();

    public SubscriptionBuilder(ISubscriptionManager subscriptionManager)
    {
        _subscriptionManager = subscriptionManager;
    }

    public ISubscriptionBuilder ForEvent<TEvent>() where TEvent : class
    {
        return ForEvent(typeof(TEvent));
    }

    public ISubscriptionBuilder ForEvent(Type eventType)
    {
        _eventType = eventType;
        return this;
    }

    public ISubscriptionBuilder WithTopic(string topicName)
    {
        _topicName = topicName;
        return this;
    }

    public ISubscriptionBuilder WithHandler<THandler>() where THandler : IMoEventHandler
    {
        return WithHandler(typeof(THandler));
    }

    public ISubscriptionBuilder WithHandler(Type handlerType)
    {
        _handlerType = handlerType;
        // Will create IocEventHandlerFactory when building
        return this;
    }

    public ISubscriptionBuilder WithHandler<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        if (_eventType == null)
        {
            _eventType = typeof(TEvent);
        }

        _handlerFactory = new ActionEventHandlerFactory<TEvent>(action);
        return this;
    }

    public ISubscriptionBuilder WithHandlerFactory(IEventHandlerFactory factory)
    {
        _handlerFactory = factory;
        return this;
    }

    public ISubscriptionBuilder WithScope(SubscriptionScope scope)
    {
        _scope = scope;
        return this;
    }

    public ISubscriptionBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public ISubscriptionBuilder WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        foreach (var kvp in metadata)
        {
            _metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }

    public ISubscriptionBuilder AsAutoDiscovered()
    {
        _isAutoDiscovered = true;
        return this;
    }

    public async Task<ISubscription> SubscribeAsync()
    {
        var descriptor = Build();
        return await _subscriptionManager.SubscribeAsync(descriptor);
    }

    public SubscriptionDescriptor Build()
    {
        if (_eventType == null)
            throw new InvalidOperationException("Event type must be specified");

        // Determine topic name
        var topicName = _topicName ?? EventNameAttribute.GetNameOrDefault(_eventType);

        // Determine handler factory
        var handlerFactory = _handlerFactory;
        if (handlerFactory == null && _handlerType != null)
        {
            // Need to create IocEventHandlerFactory, but we don't have IServiceScopeFactory here
            // This will be handled by MoEventBus Subscribe methods
            throw new InvalidOperationException(
                "Cannot create subscription with handler type using builder. " +
                "Use EventBus.Subscribe<TEvent, THandler>() instead.");
        }

        if (handlerFactory == null)
            throw new InvalidOperationException("Handler factory or action must be specified");

        // Determine scope
        var scope = _scope ?? DetermineScope();

        return new SubscriptionDescriptor
        {
            EventType = _eventType,
            TopicName = topicName,
            HandlerFactory = handlerFactory,
            Scope = scope,
            HandlerType = _handlerType,
            IsAutoDiscovered = _isAutoDiscovered,
            Metadata = _metadata.Count > 0 ? _metadata : null
        };
    }

    private SubscriptionScope DetermineScope()
    {
        if (_handlerType == null)
            return SubscriptionScope.Local; // Actions are always local

        // Check if handler implements distributed or local interface
        var interfaces = _handlerType.GetInterfaces();
        var hasDistributed = interfaces.Any(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IMoDistributedEventHandler<>));

        return hasDistributed ? SubscriptionScope.Distributed : SubscriptionScope.Local;
    }
}

/// <summary>
/// Action-based event handler factory.
/// </summary>
internal class ActionEventHandlerFactory<TEvent> : IEventHandlerFactory where TEvent : class
{
    private readonly Func<TEvent, Task> _action;

    public ActionEventHandlerFactory(Func<TEvent, Task> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = new ActionEventHandler<TEvent>(_action);
        return new EventHandlerDisposeWrapper(handler);
    }

    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories.Any(f =>
            f is ActionEventHandlerFactory<TEvent> actionFactory &&
            actionFactory._action == _action);
    }

    private class EventHandlerDisposeWrapper : IEventHandlerDisposeWrapper
    {
        public EventHandlerDisposeWrapper(IMoEventHandler eventHandler)
        {
            EventHandler = eventHandler;
        }

        public IMoEventHandler EventHandler { get; }

        public void Dispose()
        {
            // Action handlers don't need disposal
        }
    }
}

/// <summary>
/// Wraps an action as an event handler.
/// </summary>
internal class ActionEventHandler<TEvent> : IMoLocalEventHandler<TEvent> where TEvent : class
{
    private readonly Func<TEvent, Task> _action;

    public ActionEventHandler(Func<TEvent, Task> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Func<TEvent, Task> Action => _action;

    public Task HandleEventAsync(TEvent eventData)
    {
        return _action(eventData);
    }
}
