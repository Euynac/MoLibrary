namespace MoLibrary.EventBus.Abstractions.Handlers;
/// <summary>
/// This event handler is an adapter to be able to use an action as <see cref="IMoLocalEventHandler{TEvent}"/> implementation.
/// </summary>
/// <typeparam name="TEvent">Event type</typeparam>
public class ActionEventHandler<TEvent> : IMoLocalEventHandler<TEvent>
{
    /// <summary>
    /// Function to handle the event.
    /// </summary>
    public Func<TEvent, Task> Action { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ActionEventHandler{TEvent}"/>.
    /// </summary>
    /// <param name="handler">Action to handle the event</param>
    public ActionEventHandler(Func<TEvent, Task> handler) => Action = handler;

    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="eventData"></param>
    public async Task HandleEventAsync(TEvent eventData)
    {
        await Action(eventData);
    }
}

/// <summary>
/// Action-based event handler factory.
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
            // Action handlers don't need disposal
        }
    }
}