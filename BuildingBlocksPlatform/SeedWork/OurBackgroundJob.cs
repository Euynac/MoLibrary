using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using BuildingBlocksPlatform.StateStore;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

using BuildingBlocksPlatform.EventBus.Abstractions;

using BuildingBlocksPlatform.Features.MoSnowflake;
using BuildingBlocksPlatform.Transaction;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.SeedWork;



/// <summary>
/// 后台任务基类，异步执行某些工作。
/// </summary>
/// <typeparam name="TArgs"></typeparam>
public abstract class OurBackgroundJob<TArgs>(IMoServiceProvider serviceProvider) : AsyncBackgroundJob<TArgs>(serviceProvider), ITransientDependency
{
    public IServiceProvider Provider { get; set; } = serviceProvider.ServiceProvider; 
    
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger<TArgs> _logger =>
        Provider.GetRequiredService<ILogger<TArgs>>();

    /// <summary>
    /// 雪花ID生成器
    /// </summary>
    protected ISnowflakeGenerator _snowflake => Provider.GetRequiredService<ISnowflakeGenerator>()!;

    /// <summary>
    /// 对象映射器
    /// </summary>
    protected IMapper _mapper => Provider.GetRequiredService<IMapper>()!;

    /// <summary>
    /// 分布式事件总线
    /// </summary>
    protected IDistributedEventBus _domainEventBus => Provider.GetRequiredService<IDistributedEventBus>()!;

    /// <summary>
    /// 本地事件总线
    /// </summary>
    protected ILocalEventBus _localEventBus => Provider.GetRequiredService<ILocalEventBus>()!;


    /// <summary>
    /// 状态存储
    /// </summary>
    protected IStateStore _stateStore => Provider.GetRequiredService<IStateStore>()!;

   
    public sealed override async Task ExecuteAsync(TArgs args)
    {
        var manager = Provider.GetRequiredService<IMoUnitOfWorkManager>();
        using var uow = manager.Begin();
        try
        {
            await ExecuteJobAsync(args);
            await uow.CompleteAsync(); //巨坑：ABP事务需要手动提交
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
            throw;
        }
    }

    public abstract Task ExecuteJobAsync(TArgs args);
}
