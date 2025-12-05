namespace MoLibrary.EventBus.Abstractions.Handlers;

/// <summary>
/// Action-based event handler factory.
/// </summary>
internal class ActionEventHandlerFactory<TEvent>(Func<TEvent, Task> action) : IEventHandlerFactory
    where TEvent : class
{
    private readonly Func<TEvent, Task> _action = action ?? throw new ArgumentNullException(nameof(action));

    public IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = new ActionEventHandler<TEvent>(_action);
        return new EventHandlerDisposeWrapper(handler);
    }

    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories.Any(f =>
            f is ActionEventHandlerFactory<TEvent> actionFactory &&
            actionFactory._action == _action);
    }

    private class EventHandlerDisposeWrapper(IMoEventHandler eventHandler) : IEventHandlerDisposeWrapper
    {
        public IMoEventHandler EventHandler { get; } = eventHandler;

        public void Dispose()
        {
            // Action handlers don't need disposal
        }
    }
}