using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Services.Support;

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
