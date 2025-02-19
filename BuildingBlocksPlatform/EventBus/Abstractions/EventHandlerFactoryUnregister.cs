namespace BuildingBlocksPlatform.EventBus.Abstractions;

/// <summary>
/// Used to unregister a <see cref="IEventHandlerFactory"/> on <see cref="Dispose"/> method.
/// </summary>
public class EventHandlerFactoryUnRegistrar(IEventBus eventBus, Type eventType, IEventHandlerFactory factory)
    : IDisposable
{
    public void Dispose()
    {
        eventBus.Unsubscribe(eventType, factory);
    }
}