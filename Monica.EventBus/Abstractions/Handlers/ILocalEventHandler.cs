namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Handles an event delivered within the current process through Monica's local event-bus adapter.
/// </summary>
/// <typeparam name="TEvent">The handled event payload type.</typeparam>
public interface ILocalEventHandler<in TEvent> : IEventHandler<TEvent>
{
    /// <summary>
    /// Handles a local event.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    /// <param name="cancellationToken">
    /// Signals that the publisher is no longer waiting for this handler. Implementations should pass it to
    /// cancellable operations and stop promptly, but Monica cannot forcibly terminate non-cooperative code.
    /// </param>
    Task HandleEventAsync(TEvent eventData, CancellationToken cancellationToken);
}
