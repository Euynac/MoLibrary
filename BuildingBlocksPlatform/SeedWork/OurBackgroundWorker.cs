using BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;
using BuildingBlocksPlatform.Features;
using BuildingBlocksPlatform.StateStore;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocksPlatform.EventBus.Abstractions;
using BuildingBlocksPlatform.Features.MoSnowflake;

namespace BuildingBlocksPlatform.SeedWork;

public interface IOurBackgroundWorker
{
}


public abstract class OurBackgroundWorker(string cronExpression, IMoServiceProvider provider)
    : MoHangfireBackgroundWorker(cronExpression, provider), IOurBackgroundWorker
{
    public void SetQueue(string queue)
    {
        Queue = queue;
    }
}

/// <summary>
/// 后台工作者基类，继承此类并实现DoWorkAsync方法，必须给CronExpression作业执行周期。
/// </summary>
/// <typeparam name="TWorker"></typeparam>
public abstract class OurBackgroundWorker<TWorker>(string cronExpression, IMoServiceProvider provider)
    : OurBackgroundWorker(cronExpression, provider)
    where TWorker : OurBackgroundWorker<TWorker>
{
   
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger<TWorker> _logger =>
        ServiceProvider.GetRequiredService<ILogger<TWorker>>();

    /// <summary>
    /// 雪花ID生成器
    /// </summary>
    protected ISnowflakeGenerator _snowflake => ServiceProvider.GetRequiredService<ISnowflakeGenerator>()!;

    /// <summary>
    /// 对象映射器
    /// </summary>
    protected IMapper _mapper => ServiceProvider.GetRequiredService<IMapper>()!;

    /// <summary>
    /// 分布式事件总线
    /// </summary>
    protected IDistributedEventBus _domainEventBus => ServiceProvider.GetRequiredService<IDistributedEventBus>()!;

    /// <summary>
    /// 本地事件总线
    /// </summary>
    protected ILocalEventBus _localEventBus => ServiceProvider.GetRequiredService<ILocalEventBus>()!;

    /// <summary>
    /// 状态存储
    /// </summary>
    protected IStateStore _stateStore => ServiceProvider.GetRequiredService<IStateStore>()!;

    protected IMoTimekeeper _timekeeper => ServiceProvider.GetRequiredService<IMoTimekeeper>()!;


    public sealed override async Task DoWorkAsync(CancellationToken cancellationToken = default)
    {
        //https://www.cnblogs.com/wucy/p/16791563.html
        var factory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        await using var scope = factory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IMoUnitOfWorkManager>();
        using var uow = manager.Begin();
        using var _ = _timekeeper.CreateAutoTimer(typeof(TWorker).Name);
        try
        {
            await DoWorkAsync(new OurBackgroundWorkerContext(scope.ServiceProvider, cancellationToken));
            await uow.CompleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            //await scope.ServiceProvider
            //    .GetRequiredService<IExceptionNotifier>()
            //    .NotifyAsync(new ExceptionNotificationContext(ex));

            _logger.LogException(ex);
            ex.ReThrow();
        }
    }

    protected abstract Task DoWorkAsync(OurBackgroundWorkerContext workerContext);
}

/// <summary>
/// Worker上下文，包含了ServiceProvider和CancellationToken。使用ServiceProvider可以获取到Scope生命周期的所需服务，而不会与Worker单例生命周期绑定。
/// </summary>
public class OurBackgroundWorkerContext(IServiceProvider serviceProvider, CancellationToken cancellationToken)
{
    /// <summary>
    /// Scope生命周期的ServiceProvider，后台工作类需使用该方法获取服务，以保证服务的Scope生命周期。
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    public T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public OurBackgroundWorkerContext(IServiceProvider serviceProvider) : this(serviceProvider, default)
    {
    }
}


//public class Test : AsyncPeriodicBackgroundWorkerBase