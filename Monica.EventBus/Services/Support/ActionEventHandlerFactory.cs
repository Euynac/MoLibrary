using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Services.Support;
/// <summary>
/// Adapter that allows a delegate to be used as an <see cref="ILocalEventHandler{TEvent}"/>
/// implementation.
/// </summary>
/// <typeparam name="TEvent">Event type.</typeparam>
public class ActionEventHandler<TEvent> : ILocalEventHandler<TEvent>
{
    /// <summary>
    /// Delegate used to handle the event.
    /// </summary>
    public Func<TEvent, CancellationToken, Task> Action { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ActionEventHandler{TEvent}"/>.
    /// </summary>
    /// <param name="handler">Delegate that handles the event.</param>
    public ActionEventHandler(Func<TEvent, CancellationToken, Task> handler) => Action = handler;

    /// <summary>
    /// Handles the event by invoking the configured delegate.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    /// <param name="cancellationToken">Signals that the event delivery is no longer waiting.</param>
    public Task HandleEventAsync(TEvent eventData, CancellationToken cancellationToken)
    {
        return Action(eventData, cancellationToken);
    }
}

/// <summary>
/// Factory for delegate-based event handlers.
/// </summary>
internal class ActionEventHandlerFactory<TEvent>(Func<TEvent, CancellationToken, Task> action) : IEventHandlerFactory
    where TEvent : class
{
    private readonly Func<TEvent, CancellationToken, Task> _action = action ?? throw new ArgumentNullException(nameof(action));

    public IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = new ActionEventHandler<TEvent>(_action);
        return new EventHandlerDisposeWrapper(handler);
    }

    public Type? GetHandlerType()
    {
        return null;
    }

    private class EventHandlerDisposeWrapper(IEventHandler eventHandler) : IEventHandlerDisposeWrapper
    {
        public IEventHandler EventHandler { get; } = eventHandler;

        public void Dispose()
        {
            // Delegate-based handlers do not require disposal.
        }
    }
}
