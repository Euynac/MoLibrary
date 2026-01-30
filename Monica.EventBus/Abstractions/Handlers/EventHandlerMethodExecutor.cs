using Monica.Tool.Extensions;

namespace Monica.EventBus.Abstractions.Handlers;

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
        if (parameter is TEvent eventData)
        {
            return target.As<IMoLocalEventHandler<TEvent>>().HandleEventAsync(eventData);
        }

        return Task.CompletedTask;
    };
}

public class DistributedEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter) =>
    {
        if (parameter is TEvent eventData)
        {
            return target.As<IMoDistributedEventHandler<TEvent>>().HandleEventAsync(eventData);
        }

        return Task.CompletedTask;
    };
}
