namespace Monica.EventBus.Abstractions.Handlers;

public interface IMoLocalEventHandler<in TEvent> : IMoEventHandler<TEvent>
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    Task HandleEventAsync(TEvent eventData);
}
