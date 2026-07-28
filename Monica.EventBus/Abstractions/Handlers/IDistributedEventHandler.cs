using Monica.Core.Execution;

namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Handles an event delivered through a Monica distributed event-bus provider.
/// </summary>
/// <typeparam name="TEvent">The handled event payload type.</typeparam>
public interface IDistributedEventHandler<in TEvent> : IEventHandler<TEvent>, IExecutionAdapterOwnedComponent
{
    /// <summary>
    /// Handles a distributed event.
    /// </summary>
    /// <param name="eventData">Event payload.</param>
    /// <param name="cancellationToken">
    /// Signals that the message delivery is no longer waiting for this handler. Implementations should pass it
    /// to cancellable operations and stop promptly, but Monica cannot forcibly terminate non-cooperative code.
    /// </param>
    Task HandleEventAsync(TEvent eventData, CancellationToken cancellationToken);
}
