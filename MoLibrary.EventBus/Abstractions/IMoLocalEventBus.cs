namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// Defines interface of the event bus.
/// </summary>
public interface IMoLocalEventBus : IMoEventBus
{
    /// <summary>
    /// Registers to an event. 
    /// Same (given) instance of the handler is used for all event occurrences.
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="handler">Object to handle the event</param>
    IDisposable Subscribe<TEvent>(IMoLocalEventHandler<TEvent> handler)
        where TEvent : class;
}
