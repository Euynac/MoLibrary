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
        if (parameter is TEvent eventData)
        {
            return target.As<IMoLocalEventHandler<TEvent>>().HandleEventAsync(eventData);
        }

        return Task.CompletedTask;
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
        if (parameter is TEvent eventData)
        {
            return target.As<IMoDistributedEventHandler<TEvent>>().HandleEventAsync(eventData);
        }

        return Task.CompletedTask;
    };

    public Task ExecuteAsync(IMoEventHandler target, TEvent parameters)
    {
        return ExecutorAsync(target, parameters);
    }
}
