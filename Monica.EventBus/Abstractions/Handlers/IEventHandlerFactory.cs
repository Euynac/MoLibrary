namespace Monica.EventBus.Abstractions.Handlers;

/// <summary>
/// Defines an interface for factories those are responsible to create/get and release of event handlers.
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// Gets an event handler.
    /// </summary>
    /// <returns>The event handler</returns>
    IEventHandlerDisposeWrapper GetHandler();
    /// <summary>
    /// Gets the type of the event handler. if it is action event handler behind the factory, return nul.  
    /// </summary>
    /// <returns></returns>
    Type? GetHandlerType();
}
