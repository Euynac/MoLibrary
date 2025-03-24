using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using BuildingBlocksPlatform.BlobContainer;
using BuildingBlocksPlatform.StateStore;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using BuildingBlocksPlatform.Features.MoGuid;
using MoLibrary.Tool.General;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Security;
using MoLibrary.Core.Features.MoSnowflake;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.DomainDrivenDesign;

namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
/// 继承泛型而不是该基类
/// </summary>
public abstract class OurApplicationService : MoApplicationService
{
  
}


/// <summary>
/// 指示类是一个应用服务
/// </summary>
/// <typeparam name="THandler"></typeparam>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public abstract partial class
    OurApplicationService<THandler, TRequest, TResponse> : OurApplicationService, IRequestHandler<TRequest, TResponse>
    where THandler : OurApplicationService where TRequest : IRequest<TResponse>
{

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger<THandler> _logger =>
        ServiceProvider.GetRequiredService<ILogger<THandler>>();

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
    /// Guid生成器
    /// </summary>
    protected IGuidGenerator _guidGen => ServiceProvider.GetRequiredService<IGuidGenerator>()!;

    /// <summary>
    /// 状态存储
    /// </summary>
    protected IStateStore _stateStore => ServiceProvider.GetRequiredService<IStateStore>()!;

    /// <summary>
    /// 后台任务管理
    /// </summary>
    protected IBackgroundJobManager _backgroundJobManager => ServiceProvider.GetRequiredService<IBackgroundJobManager>()!;

    /// <summary>
    /// 文件存储
    /// </summary>
    protected IOurBlobContainer _blobContainer => ServiceProvider.GetRequiredService<IOurBlobContainer>()!;

    /// <summary>
    /// 当前用户信息
    /// </summary>

    protected IOurCurrentUser _currentUser => ServiceProvider.GetRequiredService<IOurCurrentUser>()!;
    /// <summary>
    /// 当前用户信息
    /// </summary>
    protected new IOurCurrentUser CurrentUser => _currentUser;
    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}