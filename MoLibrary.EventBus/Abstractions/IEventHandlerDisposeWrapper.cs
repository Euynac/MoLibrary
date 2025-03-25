namespace MoLibrary.EventBus.Abstractions;

public interface IEventHandlerDisposeWrapper : IDisposable
{
    IMoEventHandler EventHandler { get; }
}
public class EventHandlerDisposeWrapper(IMoEventHandler eventHandler, Action? disposeAction = null)
    : IEventHandlerDisposeWrapper
{
    public IMoEventHandler EventHandler { get; } = eventHandler;

    public void Dispose()
    {
        disposeAction?.Invoke();
    }
}
