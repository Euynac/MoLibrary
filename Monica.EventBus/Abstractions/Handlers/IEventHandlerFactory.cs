namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Defines a factory that creates event-handler execution scopes.
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// Creates or resolves an event handler in a new asynchronously disposable execution scope.
    /// </summary>
    /// <returns>The handler and scoped service provider for one delivery.</returns>
    ValueTask<IEventHandlerExecutionScope> CreateExecutionScopeAsync();

    /// <summary>
    /// Gets the concrete handler type when the factory is backed by a handler type.
    /// Returns <see langword="null"/> for action-based handlers.
    /// </summary>
    /// <returns>
    /// The concrete handler type, or <see langword="null"/> when the factory wraps a delegate.
    /// </returns>
    Type? GetHandlerType();
}
