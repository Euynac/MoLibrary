using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Extensions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.EventBus.Models;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Utils;

namespace MoLibrary.EventBus.Abstractions;

public abstract class EventBusBase : IMoEventBus
{
    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
    protected EventBusBase(IServiceScopeFactory serviceScopeFactory,
        IEventHandlerInvoker eventHandlerInvoker)
    {
        ServiceScopeFactory = serviceScopeFactory;
        EventHandlerInvoker = eventHandlerInvoker;
        SubscribeHandlers(GetAutoRegisteredHandlers());
    }

    protected IServiceScopeFactory ServiceScopeFactory { get; }
    protected IEventHandlerInvoker EventHandlerInvoker { get; }

    protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; } = new();
    protected ConcurrentDictionary<string, Type> EventTypes { get; } = new();

    /// <summary>
    /// for those automatically registered handlers
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<EventHandlerRegisterInfo> GetAutoRegisteredHandlers()
    {
        return [];
    }

    public virtual Type GetEventType(string eventName)
    {
        return EventTypes.GetOrDefault(eventName) ??
               throw new InvalidOperationException(
                   $"Event name {eventName} not found and can not get relative event type.");
    }

    public virtual IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
    }


    public virtual IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IMoEventHandler, new()
    {
        return Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
    }

    public virtual IDisposable Subscribe(Type eventType, IMoEventHandler handler)
    {
        return Subscribe(eventType, new SingleInstanceHandlerFactory(handler));
    }

    public virtual IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return Subscribe(typeof(TEvent), factory);
    }

    public virtual IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        EventTypes.GetOrAdd(eventName, eventType);
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
                {
                    if (!factory.IsInFactories(factories))
                    {
                        factories.Add(factory);
                    }
                }
            );

        return new EventHandlerFactoryUnRegistrar(this, eventType, factory);
    }


    public virtual void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        Check.NotNull(action, nameof(action));

        GetOrCreateHandlerFactories(typeof(TEvent))
            .Locking(factories =>
            {
                factories.RemoveAll(factory =>
                {
                    if (factory is not SingleInstanceHandlerFactory singleInstanceFactory)
                    {
                        return false;
                    }

                    if (singleInstanceFactory.HandlerInstance is not ActionEventHandler<TEvent> actionHandler)
                    {
                        return false;
                    }

                    return actionHandler.Action == action;
                });
            });
    }

    public virtual void Unsubscribe(Type eventType, IMoEventHandler handler)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
            {
                factories.RemoveAll(factory =>
                    factory is SingleInstanceHandlerFactory handlerFactory &&
                    handlerFactory.HandlerInstance == handler
                );
            });
    }

    public virtual void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Remove(factory));
    }

    public virtual void UnsubscribeAll(Type eventType)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Clear());
    }


    public virtual void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), factory);
    }


    public virtual void UnsubscribeAll<TEvent>() where TEvent : class
    {
        UnsubscribeAll(typeof(TEvent));
    }

    protected virtual List<IEventHandlerFactory> GetOrCreateHandlerFactories(Type eventType)
    {
        return HandlerFactories.GetOrAdd(
            eventType, _ => []);
    }


    public async Task BulkPublishAsync<TEvent>(IEnumerable<TEvent> eventDataList) where TEvent : class
    {
        await BulkPublishAsync(typeof(TEvent), eventDataList);
    }

    public async Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList)
    {
        await BulkPublishToEventBusAsync(eventType, eventDataList);
    }

    public Task PublishAsync<TEvent>(TEvent eventData)
        where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData);
    }

    public virtual async Task PublishAsync(
        Type eventType,
        object eventData)
    {
        await PublishToEventBusAsync(eventType, eventData);
    }

    protected abstract Task PublishToEventBusAsync(Type eventType, object eventData);

    /// <summary>
    /// Default implementation: iterates and publishes each event individually.
    /// Derived classes can override for optimized bulk publishing.
    /// </summary>
    protected virtual async Task BulkPublishToEventBusAsync(Type eventType, IEnumerable<object> eventDataList)
    {
        foreach (var eventData in eventDataList)
        {
            await PublishToEventBusAsync(eventType, eventData);
        }
    }

    public virtual async Task TriggerHandlersAsync(Type eventType, object eventData)
    {
        var exceptions = new List<Exception>();

        await TriggerHandlersAsync(eventType, eventData, exceptions);

        if (exceptions.Any())
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    protected virtual async Task TriggerHandlersAsync(Type eventType, object eventData, List<Exception> exceptions)
    {
        await new SynchronizationContextRemover();

        foreach (var handlerFactories in GetHandlerFactories(eventType))
        {
            foreach (var handlerFactory in handlerFactories.EventHandlerFactories)
            {
                await TriggerHandlerAsync(handlerFactory, handlerFactories.EventType, eventData, exceptions);
            }
        }
    }

    protected void ThrowOriginalExceptions(Type eventType, List<Exception> exceptions)
    {
        if (exceptions.Count == 1)
        {
            exceptions[0].ReThrow();
        }

        throw new AggregateException(
            "More than one error has occurred while triggering the event: " + eventType,
            exceptions
        );
    }

    /// <summary>
    /// Subscribe handlers using pre-computed registration information
    /// </summary>
    protected virtual void SubscribeHandlers(IEnumerable<EventHandlerRegisterInfo> handlers)
    {
        foreach (var handlerInfo in handlers)
        {
            // Direct subscription using pre-computed metadata - NO REFLECTION
            Subscribe(handlerInfo.EventType,
                     new IocEventHandlerFactory(ServiceScopeFactory, handlerInfo.HandlerType));

            // Register event type -> topic name mapping
            EventTypes.TryAdd(handlerInfo.TopicName, handlerInfo.EventType);
        }
    }

    protected virtual IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        var handlerFactoryList = new List<EventTypeWithEventHandlerFactories>();

        foreach (var handlerFactory in HandlerFactories.Where(hf => ShouldTriggerEventForHandler(eventType, hf.Key)))
        {
            handlerFactoryList.Add(new EventTypeWithEventHandlerFactories(handlerFactory.Key, handlerFactory.Value));
        }

        return handlerFactoryList.ToArray();
    }

    private static bool ShouldTriggerEventForHandler(Type targetEventType, Type handlerEventType)
    {
        //Should trigger same type
        if (handlerEventType == targetEventType)
        {
            return true;
        }

        //Should trigger for inherited types
        if (handlerEventType.IsAssignableFrom(targetEventType))
        {
            return true;
        }

        return false;
    }

    protected virtual async Task TriggerHandlerAsync(IEventHandlerFactory asyncHandlerFactory, Type eventType,
        object eventData, List<Exception> exceptions)
    {
        using var eventHandlerWrapper = asyncHandlerFactory.GetHandler();
        try
        {
            await InvokeEventHandlerAsync(eventHandlerWrapper.EventHandler, eventData, eventType);
        }
        catch (TargetInvocationException ex)
        {
            exceptions.Add(ex.InnerException!);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }
    }

    protected virtual Task InvokeEventHandlerAsync(IMoEventHandler eventHandler, object eventData, Type eventType)
    {
        return EventHandlerInvoker.InvokeAsync(eventHandler, eventData, eventType);
    }

    protected class EventTypeWithEventHandlerFactories(Type eventType, List<IEventHandlerFactory> eventHandlerFactories)
    {
        public Type EventType { get; } = eventType;

        public List<IEventHandlerFactory> EventHandlerFactories { get; } = eventHandlerFactories;
    }
}