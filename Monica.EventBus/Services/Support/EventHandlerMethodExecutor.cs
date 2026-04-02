using Monica.EventBus.Abstractions.Handlers;
using Monica.Tool.Extensions;

namespace Monica.EventBus.Services.Support;

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
        if (parameter is TEvent eventData)
        {
            return target.As<ILocalEventHandler<TEvent>>().HandleEventAsync(eventData);
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
            return target.As<IDistributedEventHandler<TEvent>>().HandleEventAsync(eventData);
        }

        return Task.CompletedTask;
    };
}
