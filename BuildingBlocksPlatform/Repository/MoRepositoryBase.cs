using System.Linq.Expressions;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Exceptions;
using BuildingBlocksPlatform.Repository.Interfaces;
using BuildingBlocksPlatform.Transaction;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.Repository;

public abstract class MoRepositoryBase<TEntity> :IMoBasicRepository<TEntity>, IMoServiceProviderInjector
    where TEntity : class, IMoEntity
{
    public IMoServiceProvider MoProvider { get; set; }
    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;

    public ILogger Logger => ServiceProvider.GetRequiredService<ILogger<MoRepositoryBase<TEntity>>>();
    public IMoUnitOfWorkManager UnitOfWorkManager => ServiceProvider.GetRequiredService<IMoUnitOfWorkManager>();

    public abstract Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    public virtual async Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await InsertAsync(entity, cancellationToken: cancellationToken);
        }

        if (autoSave)
        {
            await SaveChangesAsync(cancellationToken);
        }
    }

    protected virtual Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (UnitOfWorkManager?.Current != null)
        {
            return UnitOfWorkManager.Current.SaveChangesAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public abstract Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    public virtual async Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await UpdateAsync(entity, cancellationToken: cancellationToken);
        }

        if (autoSave)
        {
            await SaveChangesAsync(cancellationToken);
        }
    }

    public abstract Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    public virtual async Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await DeleteAsync(entity, cancellationToken: cancellationToken);
        }

        if (autoSave)
        {
            await SaveChangesAsync(cancellationToken);
        }
    }

    public abstract Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default);

    public abstract Task<long> GetCountAsync(CancellationToken cancellationToken = default);


    /// <summary>
    /// 使用Default Detail Func进行Include
    /// </summary>
    /// <returns></returns>
    public virtual Task<IQueryable<TEntity>> WithDetailsAsync()
    {
        return GetQueryableAsync();
    }
    private static IQueryable<TEntity> IncludeDetails(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, object>>[] propertySelectors)
    {
        if (!propertySelectors.IsNullOrEmptySet())
        {
            foreach (var propertySelector in propertySelectors)
            {
                query = query.Include(propertySelector);
            }
        }

        return query;
    }
    /// <summary>
    /// 通过重写方法进行配置WithDetail的默认行为
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    public virtual IQueryable<TEntity> DefaultDetailFunc(IQueryable<TEntity> entities)
    {
        return entities;
    }
    
    public virtual async Task<IQueryable<TEntity>> WithDetailsAsync(params Expression<Func<TEntity, object>>[] propertySelectors)
    {
        return IncludeDetails(await GetQueryableAsync(), propertySelectors);
    }
    public abstract Task<IQueryable<TEntity>> GetQueryableAsync();

    public abstract Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default);

    public async Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(predicate, includeDetails, cancellationToken);

        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity));
        }

        return entity;
    }

    public abstract Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default);

    public abstract Task DeleteDirectAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    protected virtual CancellationToken GetCancellationToken(CancellationToken preferredValue = default)
    {
        //TODO 参考ABP实现
        return preferredValue;
    }

}

public abstract class MoRepositoryBase<TEntity, TKey> : MoRepositoryBase<TEntity>, IMoBasicRepository<TEntity, TKey>
    where TEntity : class, IMoEntity<TKey>
{
    public abstract Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);

    public abstract Task<TEntity?> FindAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);

    public abstract Task<bool> ExistAsync(TKey id);

    public virtual async Task DeleteAsync(TKey id, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, cancellationToken: cancellationToken);
        if (entity == null)
        {
            return;
        }

        await DeleteAsync(entity, autoSave, cancellationToken);
    }

    public async Task DeleteManyAsync([NotNull] IEnumerable<TKey> ids, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        foreach (var id in ids)
        {
            await DeleteAsync(id, cancellationToken: cancellationToken);
        }

        if (autoSave)
        {
            await SaveChangesAsync(cancellationToken);
        }
    }
}
