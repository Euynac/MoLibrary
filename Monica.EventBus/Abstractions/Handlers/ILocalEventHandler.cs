namespace Monica.EventBus.Abstractions.Handlers;

public interface ILocalEventHandler<in TEvent> : IEventHandler<TEvent>
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    Task HandleEventAsync(TEvent eventData);
}
