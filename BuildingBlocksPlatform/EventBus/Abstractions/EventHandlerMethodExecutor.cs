using BuildingBlocksPlatform.Extensions;

namespace BuildingBlocksPlatform.EventBus.Abstractions;

public delegate Task EventHandlerMethodExecutorAsync(IEventHandler target, object parameter);

public interface IEventHandlerMethodExecutor
{
    EventHandlerMethodExecutorAsync ExecutorAsync { get; }
}

public class LocalEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter) =>
    {
        return parameter switch
        {
            TEvent signalEvent => target.As<ILocalEventHandler<TEvent>>().HandleEventAsync(signalEvent),
            IEnumerable<object> events when events.Select(p => p as TEvent).Where(p => p != null).ToList() is
            {
                Count: > 0
            } list => target.As<ILocalEventHandler<TEvent>>().HandleBulkEventAsync(list!),
            _ => Task.CompletedTask
        };
    };

    public Task ExecuteAsync(IEventHandler target, TEvent parameters)
    {
        return ExecutorAsync(target, parameters);
    }
}

public class DistributedEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter) =>
    {
        return parameter switch
        {
            TEvent signalEvent => target.As<IDistributedEventHandler<TEvent>>().HandleEventAsync(signalEvent),
            IEnumerable<object> events when events.Select(p => p as TEvent).Where(p => p != null).ToList() is
            {
                Count: > 0
            } list => target.As<IDistributedEventHandler<TEvent>>().HandleBulkEventAsync(list!),
            _ => Task.CompletedTask
        };
    };

    public Task ExecuteAsync(IEventHandler target, TEvent parameters)
    {
        return ExecutorAsync(target, parameters);
    }
}
