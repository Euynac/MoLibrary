namespace MoLibrary.EventBus.Abstractions;

public interface IMoLocalEventHandler<in TEvent> : IMoEventHandler<TEvent>
{
    /// <summary>
    /// Handler handles the event by implementing this method.
    /// </summary>
    /// <param name="eventData">Event data</param>
    Task HandleEventAsync(TEvent eventData);

    /// <summary>
    /// Handler handles the event by implementing this method.
    /// </summary>
    /// <param name="events"></param>
    Task HandleBulkEventAsync(IEnumerable<TEvent> events);
}
