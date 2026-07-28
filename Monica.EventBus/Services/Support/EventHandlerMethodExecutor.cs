using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Execution;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Monica.Tool.Extensions;

namespace Monica.EventBus.Services.Support;

internal delegate Task EventHandlerMethodExecutorAsync(
    IEventHandler target,
    object parameter,
    IEventSubscription subscription,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken);

internal interface IEventHandlerMethodExecutor
{
    EventHandlerMethodExecutorAsync ExecutorAsync { get; }
}

internal sealed class EventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    public EventHandlerMethodExecutorAsync ExecutorAsync =>
        EventHandlerExecutionAdapter<TEvent>.ExecuteAsync;
}

internal static class EventHandlerExecutionAdapter<TEvent>
    where TEvent : class
{
    private static readonly ConcurrentDictionary<(Type HandlerType, EventSubscriptionScope Scope), ExecutionDescriptor>
        DESCRIPTORS = new();

    public static async Task ExecuteAsync(
        IEventHandler target,
        object parameter,
        IEventSubscription subscription,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (parameter is not TEvent eventData)
        {
            throw new ArgumentException(
                $"Event payload type '{parameter.GetType().FullName}' does not match '{typeof(TEvent).FullName}'.",
                nameof(parameter));
        }

        var descriptor = DESCRIPTORS.GetOrAdd(
            (target.GetType(), subscription.Scope),
            static key => CreateDescriptor(key.HandlerType, key.Scope));
        var features = new ExecutionFeatureCollection();
        features.Set(new EventHandlerExecutionFeature(
            subscription.Id,
            subscription.TopicName,
            subscription.Scope,
            subscription.ServiceKey,
            subscription.IsAutoDiscovered));
        var context = new ExecutionContext<TEvent>(
            descriptor,
            eventData,
            target,
            cancellationToken,
            features);
        await serviceProvider.GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(context, async () =>
            {
                await InvokeHandlerAsync(
                        target,
                        eventData,
                        subscription.Scope,
                        cancellationToken)
                    .ConfigureAwait(false);
                return ExecutionUnit.Value;
            })
            .ConfigureAwait(false);
    }

    private static Task InvokeHandlerAsync(
        IEventHandler handler,
        TEvent eventData,
        EventSubscriptionScope scope,
        CancellationToken cancellationToken)
    {
        return scope switch
        {
            EventSubscriptionScope.Local =>
                ((ILocalEventHandler<TEvent>)handler).HandleEventAsync(eventData, cancellationToken),
            EventSubscriptionScope.Distributed =>
                ((IDistributedEventHandler<TEvent>)handler).HandleEventAsync(eventData, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported event subscription scope.")
        };
    }

    private static ExecutionDescriptor CreateDescriptor(Type handlerType, EventSubscriptionScope scope)
    {
        var contract = scope == EventSubscriptionScope.Local
            ? typeof(ILocalEventHandler<TEvent>)
            : typeof(IDistributedEventHandler<TEvent>);
        var entryMethod = handlerType.GetInterfaceMap(contract).TargetMethods.Single();
        var point = scope == EventSubscriptionScope.Local
            ? EventBusExecutionPoints.LocalHandler
            : EventBusExecutionPoints.DistributedHandler;

        return new ExecutionDescriptor(
            point,
            $"{handlerType.FullName}.{entryMethod.Name}",
            handlerType,
            entryMethod,
            typeof(TEvent),
            typeof(ExecutionUnit),
            isBusinessOperation: true,
            isLongRunning: false);
    }
}
