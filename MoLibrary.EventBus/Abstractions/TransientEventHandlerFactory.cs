namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// This <see cref="IEventHandlerFactory"/> implementation is used to handle events
/// by a transient instance object. 
/// </summary>
/// <remarks>
/// This class always creates a new transient instance of the handler type.
/// </remarks>
public class TransientEventHandlerFactory<THandler>()
    : TransientEventHandlerFactory(typeof(THandler)), IEventHandlerFactory
    where THandler : IMoEventHandler, new()
{
    protected override IMoEventHandler CreateHandler()
    {
        return new THandler();
    }
}

/// <summary>
/// This <see cref="IEventHandlerFactory"/> implementation is used to handle events
/// by a transient instance object. 
/// </summary>
/// <remarks>
/// This class always creates a new transient instance of the handler type.
/// </remarks>
public class TransientEventHandlerFactory(Type handlerType) : IEventHandlerFactory
{
    public Type HandlerType { get; } = handlerType;

    /// <summary>
    /// Creates a new instance of the handler object.
    /// </summary>
    /// <returns>The handler object</returns>
    public virtual IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = CreateHandler();
        return new EventHandlerDisposeWrapper(
            handler,
            () => (handler as IDisposable)?.Dispose()
        );
    }

    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<TransientEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }

    protected virtual IMoEventHandler CreateHandler()
    {
        return (IMoEventHandler)Activator.CreateInstance(HandlerType)!;
    }
}
