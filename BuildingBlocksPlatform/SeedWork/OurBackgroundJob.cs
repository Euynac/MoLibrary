using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.BackgroundJob.Abstract.Jobs;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features.MoSnowflake;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Repository.Transaction;
using MoLibrary.StateStore;

namespace BuildingBlocksPlatform.SeedWork;



/// <summary>
/// 后台任务基类，异步执行某些工作。
/// </summary>
/// <typeparam name="TArgs"></typeparam>
public abstract class OurBackgroundJob<TArgs>(IMoServiceProvider serviceProvider) : MoBackgroundJob<TArgs>(serviceProvider), ITransientDependency
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
    protected IMoDistributedEventBus _domainEventBus => Provider.GetRequiredService<IMoDistributedEventBus>()!;

    /// <summary>
    /// 本地事件总线
    /// </summary>
    protected IMoLocalEventBus _localEventBus => Provider.GetRequiredService<IMoLocalEventBus>()!;


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
