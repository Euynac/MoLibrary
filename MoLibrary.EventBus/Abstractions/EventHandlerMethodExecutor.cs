using MoLibrary.Core.Extensions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.EventBus.Abstractions;

public delegate Task EventHandlerMethodExecutorAsync(IMoEventHandler target, object parameter);

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
            TEvent signalEvent => target.As<IMoLocalEventHandler<TEvent>>().HandleEventAsync(signalEvent),
            IEnumerable<object> events when events.Select(p => p as TEvent).Where(p => p != null).ToList() is
            {
                Count: > 0
            } list => target.As<IMoLocalEventHandler<TEvent>>().HandleBulkEventAsync(list!),
            _ => Task.CompletedTask
        };
    };

    public Task ExecuteAsync(IMoEventHandler target, TEvent parameters)
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
            TEvent signalEvent => target.As<IMoDistributedEventHandler<TEvent>>().HandleEventAsync(signalEvent),
            IEnumerable<object> events when events.Select(p => p as TEvent).Where(p => p != null).ToList() is
            {
                Count: > 0
            } list => target.As<IMoDistributedEventHandler<TEvent>>().HandleBulkEventAsync(list!),
            _ => Task.CompletedTask
        };
    };

    public Task ExecuteAsync(IMoEventHandler target, TEvent parameters)
    {
        return ExecutorAsync(target, parameters);
    }
}
