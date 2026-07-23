using Monica.EventBus.Abstractions.Handlers;
using Monica.Tool.Extensions;

namespace Monica.EventBus.Services.Support;

internal delegate Task EventHandlerMethodExecutorAsync(
    IEventHandler target,
    object parameter,
    CancellationToken cancellationToken);

internal interface IEventHandlerMethodExecutor
{
    EventHandlerMethodExecutorAsync ExecutorAsync { get; }
}

internal sealed class LocalEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter, cancellationToken) =>
    {
        if (parameter is TEvent eventData)
        {
            return target.As<ILocalEventHandler<TEvent>>().HandleEventAsync(eventData, cancellationToken);
        }

        return Task.CompletedTask;
    };
}

internal sealed class DistributedEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter, cancellationToken) =>
    {
        if (parameter is TEvent eventData)
        {
            return target.As<IDistributedEventHandler<TEvent>>().HandleEventAsync(eventData, cancellationToken);
        }

        return Task.CompletedTask;
    };
}
