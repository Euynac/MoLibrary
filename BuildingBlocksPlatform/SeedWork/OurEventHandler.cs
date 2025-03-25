using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Repository.Transaction;
using MoLibrary.StateStore;

namespace BuildingBlocksPlatform.SeedWork;


/// <summary>
/// 事件处理抽象基类。不要继承该类，而选择继承<see cref="OurDomainEventHandler{THandler,TEvent}"/>或<see cref="OurLocalEventHandler{THandler,TEvent}"/>。
/// </summary>
public abstract class OurEventHandlerBase<THandler,TEvent> : IMoEventHandler<TEvent>, IMoServiceProviderInjector
{
    internal OurEventHandlerBase()
    {
    }
    public IMoServiceProvider MoProvider { get; set; }

    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;

    protected ILogger<THandler> _logger =>
        ServiceProvider.GetRequiredService<ILogger<THandler>>();

    protected IMapper _mapper => ServiceProvider.GetRequiredService<IMapper>()!;
    protected IMoDistributedEventBus _domainEventBus => ServiceProvider.GetRequiredService<IMoDistributedEventBus>()!;

    /// <summary>
    /// 状态存储
    /// </summary>
    protected IStateStore _stateStore => ServiceProvider.GetRequiredService<IStateStore>()!;
    protected IMoUnitOfWorkManager _uowManager => ServiceProvider.GetRequiredService<IMoUnitOfWorkManager>()!;
}

/// <summary>
///  领域事件处理抽象基类。继承该类，实现<see cref="HandleEventAsync"/>方法。
/// </summary>
/// <typeparam name="THandler"></typeparam>
/// <typeparam name="TEvent"></typeparam>
public abstract class OurDomainEventHandler<THandler, TEvent> : OurEventHandlerBase<THandler, TEvent>, IMoDistributedEventHandler<TEvent>,
    ITransientDependency where THandler : OurDomainEventHandler<THandler, TEvent>
{
    /// <summary>
    /// If you perform database operations and use the repositories inside the event handler, you may need to create a unit of work, because some repository methods need to work inside an active unit of work. Make the handle method virtual and add a [UnitOfWork] attribute for the method, or manually use the IMoUnitOfWorkManager to create a unit of work scope. https://docs.abp.io/en/abp/latest/Distributed-Event-Bus
    /// </summary>
    /// <param name="eto"></param>
    /// <returns></returns>
    public abstract Task HandleEventAsync(TEvent eto);

    public virtual async Task HandleBulkEventAsync(IEnumerable<TEvent> events)
    {
        using var uow = _uowManager.Begin();
        foreach (var e in events)
        {
            await HandleEventAsync(e);
        }
    }
}

/// <summary>
/// 本地事件处理抽象基类。继承该类，实现<see cref="HandleEventAsync"/>方法。
/// </summary>
/// <typeparam name="THandler"></typeparam>
/// <typeparam name="TEvent"></typeparam>
public abstract class OurLocalEventHandler<THandler, TEvent> : OurEventHandlerBase<THandler, TEvent>, IMoLocalEventHandler<TEvent>,
    ITransientDependency where THandler : OurLocalEventHandler<THandler, TEvent>
{
    /// <summary>
    /// If you perform database operations and use the repositories inside the event handler, you may need to create a unit of work, because some repository methods need to work inside an active unit of work. Make the handle method virtual and add a [UnitOfWork] attribute for the method, or manually use the IMoUnitOfWorkManager to create a unit of work scope. https://docs.abp.io/en/abp/latest/Distributed-Event-Bus
    /// </summary>
    /// <param name="eto"></param>
    /// <returns></returns>
    public abstract Task HandleEventAsync(TEvent eto);
    public virtual async Task HandleBulkEventAsync(IEnumerable<TEvent> events)
    {
        using var uow = _uowManager.Begin();
        foreach (var e in events)
        {
            await HandleEventAsync(e);
        }
    }
}
