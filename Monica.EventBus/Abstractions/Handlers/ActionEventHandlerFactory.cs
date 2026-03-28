namespace Monica.EventBus.Abstractions.Handlers;
/// <summary>
/// Adapter that allows a delegate to be used as an <see cref="IMoLocalEventHandler{TEvent}"/>
/// implementation.
/// </summary>
/// <typeparam name="TEvent">Event type.</typeparam>
public class ActionEventHandler<TEvent> : IMoLocalEventHandler<TEvent>
{
    /// <summary>
    /// Delegate used to handle the event.
    /// </summary>
    public Func<TEvent, Task> Action { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ActionEventHandler{TEvent}"/>.
    /// </summary>
    /// <param name="handler">Delegate that handles the event.</param>
    public ActionEventHandler(Func<TEvent, Task> handler) => Action = handler;

    /// <summary>
    /// Handles the event by invoking the configured delegate.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    public async Task HandleEventAsync(TEvent eventData)
    {
        await Action(eventData);
    }
}

/// <summary>
/// Factory for delegate-based event handlers.
/// </summary>
internal class ActionEventHandlerFactory<TEvent>(Func<TEvent, Task> action) : IEventHandlerFactory
    where TEvent : class
{
    private readonly Func<TEvent, Task> _action = action ?? throw new ArgumentNullException(nameof(action));

    public IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = new ActionEventHandler<TEvent>(_action);
        return new EventHandlerDisposeWrapper(handler);
    }

    public Type? GetHandlerType()
    {
        return null;
    }

    private class EventHandlerDisposeWrapper(IMoEventHandler eventHandler) : IEventHandlerDisposeWrapper
    {
        public IMoEventHandler EventHandler { get; } = eventHandler;

        public void Dispose()
        {
            // Delegate-based handlers do not require disposal.
        }
    }
}
