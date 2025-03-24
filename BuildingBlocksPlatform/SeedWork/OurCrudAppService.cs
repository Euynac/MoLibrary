using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using BuildingBlocksPlatform.BlobContainer;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud;
using BuildingBlocksPlatform.StateStore;
using MapsterMapper;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud.Interfaces;
using BuildingBlocksPlatform.Features.MoGuid;
using BuildingBlocksPlatform.Features.MoSnowflake;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Authority.Security;
using MoLibrary.EventBus.Abstractions;

namespace BuildingBlocksPlatform.SeedWork;


/// <summary>
/// <inheritdoc/> <para>该基类禁用修改与增加功能，需进一步禁用删除使用<see cref="IMoCrudDisableDelete"/> </para>
/// </summary>
public abstract class OurCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TRepository>(
    TRepository repository)
    : OurCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, TGetListInput, OurCrudDisableDto, OurCrudDisableDto,
        OurCrudDisableDto, TRepository>(repository)
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IHasEntityId<TKey>
    where TRepository : IOurRepository<TEntity, TKey>
{
}

/// <summary>
/// <inheritdoc/>
/// </summary>
public abstract class OurCrudAppService<TEntity, TEntityDto, TKey, TCreateInput, TUpdateInput, TRepository>(
    TRepository repository)
    : OurCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, OurCrudPageRequestDto, TCreateInput, TUpdateInput,
        OurCrudDisableDto, TRepository>(repository)
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IHasEntityId<TKey>
    where TRepository : IOurRepository<TEntity, TKey>
{
}

/// <summary>
/// <inheritdoc/>
/// </summary>
public abstract class OurCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput,
    TRepository>(TRepository repository)
    : OurCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput, OurCrudDisableDto, TRepository>(
        repository)
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IHasEntityId<TKey>
    where TRepository : IOurRepository<TEntity, TKey>
{
}

/// <summary>
/// 自动CRUD接口基类。子类必须以AppService结尾，否则无法自动注册。其余开头名字会自动生成为路由名，以小写单词短横线隔开。如UserListAppService：user-list
/// </summary>
public abstract class OurCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput,
    TUpdateInput, TBulkDeleteInput, TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput,
        TUpdateInput, TBulkDeleteInput, TRepository>(
        repository) 
    where TEntity : class, IMoEntity<TKey>
    where TGetOutputDto : IHasEntityId<TKey>
    where TGetListOutputDto : IHasEntityId<TKey>
    where TRepository : IOurRepository<TEntity, TKey>
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger _logger => ServiceProvider
        .GetRequiredService<ILogger<OurCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput,
            TCreateInput,
            TUpdateInput, TBulkDeleteInput, TRepository>>>();

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
    /// 文件存储
    /// </summary>
    protected IOurBlobContainer _blobContainer => ServiceProvider.GetRequiredService<IOurBlobContainer>()!;

    /// <summary>
    /// 后台任务管理
    /// </summary>
    protected IBackgroundJobManager _backgroundJobManager =>
        ServiceProvider.GetRequiredService<IBackgroundJobManager>()!;

    /// <summary>
    /// 自动模型
    /// </summary>
    protected IAutoModelDbOperator<TEntity> _autoDb =>
        ServiceProvider.GetRequiredService<IAutoModelDbOperator<TEntity>>()!;
    /// <summary>
    /// 当前用户信息
    /// </summary>

    protected IOurCurrentUser _currentUser => ServiceProvider.GetRequiredService<IOurCurrentUser>()!;
    /// <summary>
    /// 当前用户信息
    /// </summary>
    protected new IOurCurrentUser CurrentUser => _currentUser;
  
}