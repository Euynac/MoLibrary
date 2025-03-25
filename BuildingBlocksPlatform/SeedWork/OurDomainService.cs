using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Authority.Security;
using MoLibrary.Core.Features.MoSnowflake;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.DomainDrivenDesign;
using MoLibrary.Framework.Features.MoGuid;
using MoLibrary.StateStore;


namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
/// 指示类是一个领域服务
/// </summary>
/// <typeparam name="TService"></typeparam>
public abstract class OurDomainService<TService> : MoDomainService<TService> where TService : MoDomainService<TService>, IMoServiceProviderInjector
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger<TService> _logger =>
        ServiceProvider.GetRequiredService<ILogger<TService>>();
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
    protected IMoDistributedEventBus _domainEventBus => ServiceProvider.GetRequiredService<IMoDistributedEventBus>()!;

    /// <summary>
    /// 本地事件总线
    /// </summary>
    protected IMoLocalEventBus _localEventBus => ServiceProvider.GetRequiredService<IMoLocalEventBus>()!;

    /// <summary>
    /// Guid生成器
    /// </summary>
    protected IGuidGenerator _guidGen => ServiceProvider.GetRequiredService<IGuidGenerator>()!;

    /// <summary>
    /// 状态存储
    /// </summary>
    protected IStateStore _stateStore => ServiceProvider.GetRequiredService<IStateStore>()!;

    /// <summary>
    /// 当前用户信息
    /// </summary>

    protected IOurCurrentUser _currentUser => ServiceProvider.GetRequiredService<IOurCurrentUser>()!;
    /// <summary>
    /// 当前用户信息
    /// </summary>
    protected new IOurCurrentUser CurrentUser => _currentUser;
}