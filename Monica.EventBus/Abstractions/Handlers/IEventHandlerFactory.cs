namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Defines a factory that creates event handlers and releases any related resources.
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// Creates or resolves an event handler instance.
    /// </summary>
    /// <returns>A wrapper around the event handler instance.</returns>
    IEventHandlerDisposeWrapper GetHandler();

    /// <summary>
    /// Gets the concrete handler type when the factory is backed by a handler type.
    /// Returns <see langword="null"/> for action-based handlers.
    /// </summary>
    /// <returns>
    /// The concrete handler type, or <see langword="null"/> when the factory wraps a delegate.
    /// </returns>
    Type? GetHandlerType();
}
