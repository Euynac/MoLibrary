namespace MoLibrary.EventBus.Abstractions;

public interface IEventHandlerDisposeWrapper : IDisposable
{
    IEventHandler EventHandler { get; }
}
public class EventHandlerDisposeWrapper(IEventHandler eventHandler, Action? disposeAction = null)
    : IEventHandlerDisposeWrapper
{
    public IEventHandler EventHandler { get; } = eventHandler;

    public void Dispose()
    {
        disposeAction?.Invoke();
    }
}
