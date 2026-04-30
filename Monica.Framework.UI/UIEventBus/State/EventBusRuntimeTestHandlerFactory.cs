using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Monica.EventBus.Services.Support;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// Creates runtime test handlers for row-level EventBus listener sessions.
/// </summary>
internal sealed class EventBusRuntimeTestHandlerFactory(
    Type eventType,
    EventSubscriptionScope scope,
    Func<object, Task> onEvent) : IEventHandlerFactory
{
    private readonly Type _eventType = eventType;
    private readonly EventSubscriptionScope _scope = scope;
    private readonly Func<object, Task> _onEvent = onEvent;

    /// <inheritdoc />
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var handlerType = _scope == EventSubscriptionScope.Local
            ? typeof(EventBusRuntimeLocalTestHandler<>).MakeGenericType(_eventType)
            : typeof(EventBusRuntimeDistributedTestHandler<>).MakeGenericType(_eventType);

        var handler = (IEventHandler)Activator.CreateInstance(handlerType, _onEvent)!;
        return new EventHandlerDisposeWrapper(handler);
    }

    /// <inheritdoc />
    public Type? GetHandlerType()
    {
        return null;
    }
}
