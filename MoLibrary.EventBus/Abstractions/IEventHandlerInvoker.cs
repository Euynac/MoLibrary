using System.Collections.Concurrent;

namespace MoLibrary.EventBus.Abstractions;

public interface IEventHandlerInvoker
{
    Task InvokeAsync(IMoEventHandler eventHandler, object eventData, Type eventType);
}
public class EventHandlerInvokerCacheItem
{
    public IEventHandlerMethodExecutor? Local { get; set; }

    public IEventHandlerMethodExecutor? Distributed { get; set; }
}

public class EventHandlerInvoker : IEventHandlerInvoker
{
    private readonly ConcurrentDictionary<string, EventHandlerInvokerCacheItem> _cache = new();

    public async Task InvokeAsync(IMoEventHandler eventHandler, object eventData, Type eventType)
    {
        var cacheItem = _cache.GetOrAdd($"{eventHandler.GetType().FullName}-{eventType.FullName}", _ =>
        {
            var item = new EventHandlerInvokerCacheItem();

            if (typeof(IMoLocalEventHandler<>).MakeGenericType(eventType).IsInstanceOfType(eventHandler))
            {
                item.Local = (IEventHandlerMethodExecutor?) Activator.CreateInstance(typeof(LocalEventHandlerMethodExecutor<>).MakeGenericType(eventType));
            }

            if (typeof(IMoDistributedEventHandler<>).MakeGenericType(eventType).IsInstanceOfType(eventHandler))
            {
                item.Distributed = (IEventHandlerMethodExecutor?) Activator.CreateInstance(typeof(DistributedEventHandlerMethodExecutor<>).MakeGenericType(eventType));
            }

            return item;
        });

        if (cacheItem.Local != null)
        {
            await cacheItem.Local.ExecutorAsync(eventHandler, eventData);
        }

        if (cacheItem.Distributed != null)
        {
            await cacheItem.Distributed.ExecutorAsync(eventHandler, eventData);
        }

        if (cacheItem.Local == null && cacheItem.Distributed == null)
        {
            throw new Exception("The object instance is not an event handler. Object type: " + eventHandler.GetType().AssemblyQualifiedName);
        }
    }
}