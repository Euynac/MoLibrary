namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// This <see cref="IEventHandlerFactory"/> implementation is used to handle events
/// by a single instance object. 
/// </summary>
/// <remarks>
/// This class always gets the same single instance of handler.
/// </remarks>
/// <remarks>
/// 
/// </remarks>
/// <param name="handler"></param>
public class SingleInstanceHandlerFactory(IMoEventHandler handler) : IEventHandlerFactory
{
    /// <summary>
    /// The event handler instance.
    /// </summary>
    public IMoEventHandler HandlerInstance { get; } = handler;

    public IEventHandlerDisposeWrapper GetHandler()
    {
        return new EventHandlerDisposeWrapper(HandlerInstance);
    }

    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<SingleInstanceHandlerFactory>()
            .Any(f => f.HandlerInstance == HandlerInstance);
    }
}
