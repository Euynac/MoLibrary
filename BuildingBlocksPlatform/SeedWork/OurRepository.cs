using System.Linq.Expressions;
using BuildingBlocksPlatform.Authority.Security;
using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.Features.MoGuid;
using BuildingBlocksPlatform.Features.MoSnowflake;
using BuildingBlocksPlatform.Repository;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Interfaces;
using BuildingBlocksPlatform.StateStore;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using ShardingCore.Sharding.Abstractions;






namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
/// 仓库基类。注意，命名必须以Repository开头才能自动注册，后面加实体Entity名，如RepositoryFlight。
/// </summary>
/// <typeparam name="TDbContext"></typeparam>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TKey"></typeparam>
public abstract class OurRepository<TDbContext, TEntity, TKey>(IDbContextProvider<TDbContext> dbContextProvider) :
    MoRepository<TDbContext, TEntity, TKey>(dbContextProvider),
    IOurRepository<TEntity, TKey>, IOurRepositoryCache where TDbContext : MoDbContext<TDbContext>
    where TEntity : class, IMoEntity<TKey>
{
  

    protected ILogger<TDbContext> _logger =>
        ServiceProvider.GetRequiredService<ILogger<TDbContext>>();

    /// <summary>
    /// 雪花ID生成器
    /// </summary>
    protected ISnowflakeGenerator _snowflake => ServiceProvider.GetRequiredService<ISnowflakeGenerator>()!;

    /// <summary>
    /// Guid生成器
    /// </summary>
    protected IGuidGenerator _guidGen => ServiceProvider.GetRequiredService<IGuidGenerator>()!;

    /// <summary>
    /// 状态存储
    /// </summary>
    protected IStateStore _stateStore => ServiceProvider.GetRequiredService<IStateStore>()!;

    /// <summary>
    /// 对象映射器
    /// </summary>
    protected IMapper _mapper => ServiceProvider.GetRequiredService<IMapper>()!;
    /// <summary>
    /// 当前用户信息
    /// </summary>

    protected IOurCurrentUser _currentUser => ServiceProvider.GetRequiredService<IOurCurrentUser>()!;
    /// <summary>
    /// 当前用户信息
    /// </summary>
    protected new IOurCurrentUser CurrentUser => _currentUser;

    public async Task<DebugView> GetDebugView()
    {
        return (await GetDbContextAsync()).ChangeTracker.DebugView;
    }

    public virtual bool IsShardingTable()
    {
        return typeof(TDbContext).IsImplementInterface<IShardingTableDbContext>();
    }

    //https://github.com/borisdj/EFCore.BulkExtensions

    public virtual async Task<TEntity> UpdateAsync(TEntity entity, Func<TEntity, Task> updateMethod,
        bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        dbContext.Attach(entity);
        await updateMethod.Invoke(entity);
        var updatedEntity = dbContext.Update(entity).Entity;

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
        }

        return updatedEntity;
    }

    public override Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        return base.DeleteAsync(predicate, autoSave, cancellationToken);
    }

    public virtual async Task<TEntity> UpdateAsync(TEntity entity, Action<TEntity> updateMethod, bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        dbContext.Attach(entity);
        updateMethod.Invoke(entity);
        var updatedEntity = dbContext.Update(entity).Entity;

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
        }

        return updatedEntity;
    }

    public virtual async Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AsQueryable().Where(predicate)
            .ExecuteUpdateAsync(setPropertyCalls, cancellationToken);
    }    
    public virtual async Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AsQueryable().Where(predicate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    #region 跟踪优化

    public async Task<TEntity?> GetAsync(bool asNoTracking, Expression<Func<TEntity, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        if (includeDetails)
            return await (await WithDetailsAsync(asNoTracking)).Where(predicate).SingleOrDefaultAsync(GetCancellationToken(cancellationToken));
        return await (await GetQueryableAsync(asNoTracking)).Where(predicate).SingleOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<TEntity?> GetAsync(bool asNoTracking, TKey id, bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        TEntity? result;
        if (includeDetails)
        {
            result = await(await WithDetailsAsync(asNoTracking)).OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.Id.Equals(id), GetCancellationToken(cancellationToken));
        }
        else
        {
            TEntity? entity;
            if (asNoTracking)
                entity = await(await GetQueryableAsync(asNoTracking)).OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.Id.Equals(id), GetCancellationToken(cancellationToken));
            else
                entity = await(await GetDbSetAsync()).FindAsync([id], GetCancellationToken(cancellationToken));
            result = entity;
        }
        return result;
    }

    public override async Task<IQueryable<TEntity>> WithDetailsAsync()
    {
        return await WithDetailsAsync(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <returns></returns>
    public virtual async Task<IQueryable<TEntity>> WithDetailsAsync(bool asNoTracking)
    {
        return DefaultDetailFunc(await GetQueryableAsync(asNoTracking));
    }


    public override async Task<IQueryable<TEntity>> GetQueryableAsync()
    {
        return await GetQueryableAsync(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <returns></returns>
   
    public virtual async Task<IQueryable<TEntity>> GetQueryableAsync(bool asNoTracking)
    {
        return asNoTracking
            ? (await GetDbSetAsync()).AsQueryable().AsNoTracking()
            : (await GetDbSetAsync()).AsQueryable();
    }
  
    public async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool asNoTracking, bool includeDetails = false, CancellationToken cancellationToken = default)
    {
        List<TEntity> listAsync;
        if (includeDetails)
            listAsync = await (await WithDetailsAsync(asNoTracking)).Where(predicate).ToListAsync(GetCancellationToken(cancellationToken));
        else
            listAsync = await (await GetQueryableAsync(asNoTracking)).Where(predicate).ToListAsync(GetCancellationToken(cancellationToken));
        return listAsync;
    }
 
    public async Task<List<TEntity>> GetListAsync(bool asNoTracking, bool includeDetails = false, CancellationToken cancellationToken = default)
    {

        List<TEntity> listAsync;
        if (includeDetails)
            listAsync = await (await WithDetailsAsync(asNoTracking)).ToListAsync(GetCancellationToken(cancellationToken));
        else
            listAsync = await (await GetQueryableAsync(asNoTracking)).ToListAsync(GetCancellationToken(cancellationToken));
        return listAsync;
    }
    public override async Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
    {
        return await GetListAsync(true, includeDetails, cancellationToken);
    }

    public override async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default)
    {
        return await GetListAsync(predicate, true, includeDetails, cancellationToken);
    }

    #endregion

    #region  缓存

    public virtual Task<IEnumerable<TEntity>> GetCachedListAsync()
    {
        throw new NotImplementedException($"{typeof(TEntity).FullName}仓储层未实现缓存");
    }

    public async Task<List<TEntity>> GetCachedListAsync(Func<TEntity, bool> predicate)
    {
        var list = await GetCachedListAsync();
        return list.Where(predicate).ToList();
    }
    protected static readonly AsyncReaderWriterLock _lock = new();

    protected static DateTime? version = null;
    public virtual async Task GetVersion()
    {

        var v = typeof(TEntity).Name;
        var remoteVersion = await _stateStore.GetStateAsync<DateTime?>(typeof(TEntity).Name);

        if (remoteVersion == null)
        {
            await UpdateCacheVersion();
        }
        else if (!remoteVersion.EqualBySecond(version))
        {
            using (var l = await _lock.ReaderLockAsync())
            {
                await RefreshCacheAsync();
                version =  remoteVersion;
            }
        }
    }

    public virtual async Task UpdateCacheVersion()
    {
        version =  DateTime.Now;
        await _stateStore.SaveStateAsync(typeof(TEntity).Name, version!);
        await RefreshCacheAsync();
    }

    //制空缓存dict
    public virtual  Task RefreshCacheAsync()
    {
        return Task.CompletedTask;
    }
    #endregion
}

/// <summary>
/// 该基类以雪花ID为主键。<inheritdoc/>
/// </summary>
/// <typeparam name="TDbContext"></typeparam>
/// <typeparam name="TEntity"></typeparam>
public abstract class OurRepository<TDbContext, TEntity>
    (IDbContextProvider<TDbContext> dbContextProvider) : OurRepository<TDbContext, TEntity, long>(dbContextProvider)
    where TDbContext : MoDbContext<TDbContext>
    where TEntity : class, IMoEntity<long>;

/// <summary>
/// 仓库缓存方法
/// </summary>
public interface IOurRepositoryCache
{
    /// <summary>
    /// 刷新缓存
    /// </summary>
    Task RefreshCacheAsync();

    Task UpdateCacheVersion();
}
/// <summary>
/// 仓库方法接口标记
/// </summary>
public interface IOurRepository : IOurRepositoryCache
{

    /// <summary>
    /// 获取当前ChangeTracker DebugView
    /// </summary>
    /// <returns></returns>
    public Task<DebugView> GetDebugView();


    /// <summary>
    /// 该仓库是进行了分表操作
    /// </summary>
    /// <returns></returns>
    public bool IsShardingTable();
    //巨坑：internal类型的接口方法，会出现很多莫名其妙的问题。比如OurRepo实现了，但是在子类还是被看作未被实现（通过了编译，但是运行时Castle激活会发现并未实现）如果给个接口默认实现，则会进来接口默认实现而不会进到OurRepo的实现。

    Task DynamicInsertAsync(dynamic entity);
    Task DynamicUpdateAsync(dynamic entity);
    Task DynamicDeleteAsync(dynamic entity);
    Task<dynamic?> DynamicFindAsync(dynamic entity);
    Task<dynamic?> DynamicFindAsync(long entityId);
    Task<bool> DynamicExistAsync(dynamic entity);
    Task DynamicInsertManyAsync(IEnumerable<dynamic> entities);
    Task DynamicUpdateManyAsync(IEnumerable<dynamic> entities);
    Task DynamicDeleteManyAsync(IEnumerable<dynamic> entities);
}

/// <summary>
/// 仓库方法接口
/// </summary>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TKey"></typeparam>
public interface IOurRepository<TEntity, TKey> : IMoRepository<TEntity, TKey>, IOurRepository where TEntity : class, IMoEntity<TKey>
{
    async Task<dynamic?> IOurRepository.DynamicFindAsync(dynamic entity)
    {
        if (entity is IOurEntity<TKey> entityWithId)
        {
            return await FindAsync(entityWithId.Id);
        }

        return null;
    }

    async Task<dynamic?> IOurRepository.DynamicFindAsync(long entityId)
    {
        if (entityId is TKey key)
        {
            return await FindAsync(key);
        }

        return null;
    }

    async Task<bool> IOurRepository.DynamicExistAsync(dynamic entity)
    {
        if (entity is IOurEntity<TKey> entityWithId)
        {
            return await ExistAsync(entityWithId.Id);
        }

        throw new ArgumentException("实体类必须继承接口IOurEntity");
    }

    async Task IOurRepository.DynamicInsertAsync(dynamic entity)
    {
        await InsertAsync(entity, true);
    }

    async Task IOurRepository.DynamicUpdateAsync(dynamic entity)
    {
        await UpdateAsync(entity, true);
    }
    async Task IOurRepository.DynamicDeleteAsync(dynamic entity)
    {
        await DeleteAsync(entity, true);
    }
    async Task IOurRepository.DynamicInsertManyAsync(IEnumerable<dynamic> entity)
    {
        await InsertManyAsync((dynamic)entity, true);
    }

    async Task IOurRepository.DynamicUpdateManyAsync(IEnumerable<dynamic> entity)
    {
        await UpdateManyAsync((dynamic) entity, true);
    }
    async Task IOurRepository.DynamicDeleteManyAsync(IEnumerable<dynamic> entity)
    {
        await DeleteManyAsync((dynamic) entity, true);
    }


    /// <summary>
    /// 更新实体。原方法需要GetAsync后开启追踪特性再进行Update，这里通过Attach进行追踪，传入Action进行Update
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="updateMethod"></param>
    /// <param name="autoSave"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity> UpdateAsync(TEntity entity, Func<TEntity, Task> updateMethod, bool autoSave = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体。原方法需要GetAsync后开启追踪特性再进行Update，这里通过Attach进行追踪，传入Action进行Update
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="updateMethod"></param>
    /// <param name="autoSave"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity> UpdateAsync(TEntity entity, Action<TEntity> updateMethod, bool autoSave = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 条件批量更新
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="setPropertyCalls"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>> predicate,
        Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 条件批量删除
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取缓存列表（需仓储本身支持，否则直接抛出异常）
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<TEntity>> GetCachedListAsync();

    /// <summary>
    /// 获取缓存列表（需仓储本身支持，否则直接抛出异常）
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    Task<List<TEntity>> GetCachedListAsync(Func<TEntity, bool> predicate);


    [Obsolete("使用GetQueryableAsync(bool asNoTracking)以替代")]
    new Task<IQueryable<TEntity>> GetQueryableAsync();

    [Obsolete("使用GetAsync(bool asNoTracking)以替代")]
    new Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// 可自主控制跟踪，获取指定Id相关对象。
    /// </summary>
    /// <param name="asNoTracking"></param>
    /// <param name="id"></param>
    /// <param name="includeDetails"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>获取失败返回null</returns>
    Task<TEntity?> GetAsync(bool asNoTracking, TKey id, bool includeDetails = true,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 可自主控制跟踪，获取指定Predicate相关对象。
    /// </summary>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <param name="predicate"></param>
    /// <param name="includeDetails"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> GetAsync(bool asNoTracking, Expression<Func<TEntity, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <returns></returns>
    Task<IQueryable<TEntity>> WithDetailsAsync(bool asNoTracking);
    /// <summary>
    /// 可自主控制跟踪，获取Queryable对象。
    /// </summary>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <returns></returns>
    Task<IQueryable<TEntity>> GetQueryableAsync(bool asNoTracking);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <param name="includeDetails"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<TEntity>> GetListAsync(bool asNoTracking, bool includeDetails = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="asNoTracking">是否开启跟踪，若需对结果进行修改并保存，则需要开启跟踪。只读操作无需跟踪，可大幅增强效率。</param>
    /// <param name="includeDetails"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool asNoTracking,
        bool includeDetails = false, CancellationToken cancellationToken = default);
   
}

/// <summary>
/// 仓库方法接口，以雪花ID为主键。
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IOurRepository<TEntity> : IOurRepository<TEntity, long> where TEntity : class, IMoEntity<long>;