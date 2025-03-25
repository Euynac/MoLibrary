namespace MoLibrary.EventBus.Abstractions;

public interface IMoDistributedEventBus : IMoEventBus
{
    /// <summary>
    /// Registers to an event. 
    /// Same (given) instance of the handler is used for all event occurrences.
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="handler">Object to handle the event</param>
    IDisposable Subscribe<TEvent>(IMoDistributedEventHandler<TEvent> handler)
        where TEvent : class;
}
