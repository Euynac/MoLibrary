using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Extensions;
using MoLibrary.EventBus.Attributes;
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
        SubscribeHandlers(GetDefaultHandlers());
    }

    protected IServiceScopeFactory ServiceScopeFactory { get; }
    protected IEventHandlerInvoker EventHandlerInvoker { get; }

    protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; } = new();
    protected ConcurrentDictionary<string, Type> EventTypes { get; } = new();

    /// <summary>
    /// for those automatically registered handlers
    /// </summary>
    /// <returns></returns>
    public virtual ITypeList<IMoEventHandler> GetDefaultHandlers()
    {
        return new TypeList<IMoEventHandler>();
    }

    public virtual Type GetEventType(string eventName)
    {
        return EventTypes.GetOrDefault(eventName)!;
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
    }


    /// <inheritdoc/>
    public virtual IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IMoEventHandler, new()
    {
        return Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(Type eventType, IMoEventHandler handler)
    {
        return Subscribe(eventType, new SingleInstanceHandlerFactory(handler));
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return Subscribe(typeof(TEvent), factory);
    }

    /// <inheritdoc/>
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
                factories.RemoveAll(
                    factory =>
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
                factories.RemoveAll(
                    factory =>
                        factory is SingleInstanceHandlerFactory handlerFactory &&
                        handlerFactory.HandlerInstance == handler
                );
            });
    }

    /// <inheritdoc/>
    public virtual void Unsubscribe<TEvent>(IMoLocalEventHandler<TEvent> handler) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), handler);
    }
    /// <inheritdoc/>
    public virtual void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Remove(factory));
    }

    /// <inheritdoc/>
    public virtual void UnsubscribeAll(Type eventType)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Clear());
    }

  
    /// <inheritdoc/>
    public virtual void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), factory);
    }


    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public Task PublishAsync<TEvent>(TEvent eventData)
        where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData);
    }

    /// <inheritdoc/>
    public virtual async Task PublishAsync(
        Type eventType,
        object eventData)
    {
        await PublishToEventBusAsync(eventType, eventData);
    }

    protected abstract Task PublishToEventBusAsync(Type eventType, object eventData);
    protected virtual async Task BulkPublishToEventBusAsync(Type eventType, IEnumerable<object> eventDataList)
    {
        foreach (var o in eventDataList)
        {
            await PublishToEventBusAsync(eventType, o);
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

    protected virtual void SubscribeHandlers(ITypeList<IMoEventHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            var interfaces = handler.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                if (!typeof(IMoEventHandler).GetTypeInfo().IsAssignableFrom(@interface))
                {
                    continue;
                }

                var genericArgs = @interface.GetGenericArguments();
                if (genericArgs.Length == 1)
                {
                    Subscribe(genericArgs[0], new IocEventHandlerFactory(ServiceScopeFactory, handler));
                }
            }
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
            var handlerType = eventHandlerWrapper.EventHandler.GetType();

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

    // Reference from
    // https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/
    protected struct SynchronizationContextRemover : INotifyCompletion
    {
        public bool IsCompleted => SynchronizationContext.Current == null;

        public void OnCompleted(Action continuation)
        {
            var prevContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        public SynchronizationContextRemover GetAwaiter()
        {
            return this;
        }

        public void GetResult()
        {
        }
    }
}
