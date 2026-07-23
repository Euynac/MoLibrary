using System.Collections.Concurrent;
using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Services.Support;

/// <summary>
/// Invokes a resolved event handler for a runtime event type.
/// </summary>
public interface IEventHandlerInvoker
{
    /// <summary>
    /// Invokes every local or distributed handler contract implemented for the supplied event type.
    /// </summary>
    /// <param name="eventHandler">The resolved handler instance.</param>
    /// <param name="eventData">The event payload.</param>
    /// <param name="eventType">The exact event type used for dispatch.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs the handler result.</param>
    Task InvokeAsync(
        IEventHandler eventHandler,
        object eventData,
        Type eventType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Invokes strongly typed event-handler methods while caching their runtime adapters.
/// </summary>
public sealed class EventHandlerInvoker : IEventHandlerInvoker
{
    private readonly ConcurrentDictionary<(Type HandlerType, Type EventType), HandlerExecutors> _cache = new();

    /// <inheritdoc />
    public async Task InvokeAsync(
        IEventHandler eventHandler,
        object eventData,
        Type eventType,
        CancellationToken cancellationToken = default)
    {
        var executors = _cache.GetOrAdd(
            (eventHandler.GetType(), eventType),
            static key => CreateExecutors(key.HandlerType, key.EventType));

        if (executors.Local is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await executors.Local.ExecutorAsync(eventHandler, eventData, cancellationToken);
        }

        if (executors.Distributed is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await executors.Distributed.ExecutorAsync(eventHandler, eventData, cancellationToken);
        }

        if (executors.Local is null && executors.Distributed is null)
        {
            throw new InvalidOperationException(
                $"The object instance is not an event handler. Object type: {eventHandler.GetType().AssemblyQualifiedName}");
        }
    }

    private static HandlerExecutors CreateExecutors(Type handlerType, Type eventType)
    {
        return new HandlerExecutors(
            CreateExecutorIfImplemented(
                handlerType,
                typeof(ILocalEventHandler<>).MakeGenericType(eventType),
                typeof(LocalEventHandlerMethodExecutor<>),
                eventType),
            CreateExecutorIfImplemented(
                handlerType,
                typeof(IDistributedEventHandler<>).MakeGenericType(eventType),
                typeof(DistributedEventHandlerMethodExecutor<>),
                eventType));
    }

    private static IEventHandlerMethodExecutor? CreateExecutorIfImplemented(
        Type handlerType,
        Type handlerContract,
        Type executorType,
        Type eventType)
    {
        if (!handlerContract.IsAssignableFrom(handlerType))
        {
            return null;
        }

        return (IEventHandlerMethodExecutor?)Activator.CreateInstance(executorType.MakeGenericType(eventType));
    }

    private sealed record HandlerExecutors(
        IEventHandlerMethodExecutor? Local,
        IEventHandlerMethodExecutor? Distributed);
}
