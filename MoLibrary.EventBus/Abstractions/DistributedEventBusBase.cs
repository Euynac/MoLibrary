using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.EventBus.Abstractions;

public abstract class DistributedEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker) : EventBusBase(
    serviceScopeFactory,
    eventHandlerInvoker), IMoDistributedEventBus
{
    public override async Task PublishAsync(
        Type eventType,
        object eventData)
    {
        await PublishToEventBusAsync(eventType, eventData);
    }
    
}


/// <summary>
/// 空的分布式事件总线，用于测试或不需要实际发布事件的场景
/// </summary>
public sealed class NullDistributedEventBus(IServiceScopeFactory serviceScopeFactory, IEventHandlerInvoker eventHandlerInvoker) : DistributedEventBusBase(serviceScopeFactory, eventHandlerInvoker)
{
    protected override Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }
}