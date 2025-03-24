using BuildingBlocksPlatform.Features.MoGuid;
using BuildingBlocksPlatform.StateStore;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoSnowflake;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Repository.Transaction;

namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
///     继承泛型而不是该基类
/// </summary>
public abstract class OurActor : Actor
{
    protected readonly IServiceProvider _lazyServiceProvider;

    internal OurActor(ActorHost host, IServiceProvider serviceProvider) : base(host)
    {
        _lazyServiceProvider = serviceProvider;
    }

    //TODO 参考ITransientCachedServiceProvider 提升性能

    /// <summary>
    ///     获取脱离Actor生命周期的短暂服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected T GetRequiredTransient<T>() where T : notnull
    {
        return _lazyServiceProvider.GetRequiredService<T>();
    }
}

/// <summary>
///     Actor基类
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class OurActor<T>(
    ActorHost host,
    IServiceProvider serviceProvider) : OurActor(host, serviceProvider)
    where T : OurActor<T>, IActor
{
    /// <summary>
    ///     日志记录器
    /// </summary>
    protected ILogger<T> _logger => _lazyServiceProvider.GetRequiredService<ILogger<T>>();

    /// <summary>
    ///     雪花ID生成器
    /// </summary>
    protected ISnowflakeGenerator _snowflake => _lazyServiceProvider.GetRequiredService<ISnowflakeGenerator>()!;

    /// <summary>
    ///     对象映射器
    /// </summary>
    protected IMapper _mapper => _lazyServiceProvider.GetRequiredService<IMapper>()!;

    /// <summary>
    ///     分布式事件总线
    /// </summary>
    protected IDistributedEventBus _domainEventBus => _lazyServiceProvider.GetRequiredService<IDistributedEventBus>()!;

    /// <summary>
    ///     本地事件总线
    /// </summary>
    protected ILocalEventBus _localEventBus => _lazyServiceProvider.GetRequiredService<ILocalEventBus>()!;

    /// <summary>
    ///     Guid生成器
    /// </summary>
    protected IGuidGenerator _guidGen => _lazyServiceProvider.GetRequiredService<IGuidGenerator>()!;

    /// <summary>
    ///     状态存储
    /// </summary>
    protected IStateStore _stateStore => _lazyServiceProvider.GetRequiredService<IStateStore>()!;

    //TODO 测试是否可以使用IMoUnitOfWorkManager，以及与IRepository的兼容
    protected IMoUnitOfWorkManager _unitOfWorkManager =>
        _lazyServiceProvider.GetRequiredService<IMoUnitOfWorkManager>()!;
}