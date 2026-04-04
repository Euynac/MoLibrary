using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Exceptions;
using Monica.Tool.Extensions;

namespace Monica.Repository.Persistence.Services;

public class EfRepository<TDbContext, TEntity>(
    IDbContextProvider<TDbContext> dbContextProvider)
    : RepositoryBase<TEntity>, IRepository<TEntity>
    where TDbContext : RepositoryDbContext<TDbContext>
    where TEntity : class, IEntity
{
    async Task<DbContext> IRepository.GetDbContextAsync()
    {
        return await GetDbContextAsync();
    }

    protected virtual Task<TDbContext> GetDbContextAsync()
    {
        return dbContextProvider.GetDbContextAsync();
    }
    protected override async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await (await GetDbContextAsync()).SaveChangesAsync(cancellationToken);
    }

    public async Task<int> SaveChanges(CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
    Task<DbSet<TEntity>> IRepository<TEntity>.GetDbSetAsync()
    {
        return GetDbSetAsync();
    }
    public virtual async Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>> predicate, Action<UpdateSettersBuilder<TEntity>> setPropertyCalls,
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

    protected async Task<DbSet<TEntity>> GetDbSetAsync()
    {
        return (await GetDbContextAsync()).Set<TEntity>();
    }

    protected async Task<IDbConnection> GetDbConnectionAsync()
    {
        return (await GetDbContextAsync()).Database.GetDbConnection();
    }

    protected async Task<IDbTransaction?> GetDbTransactionAsync()
    {
        return (await GetDbContextAsync()).Database.CurrentTransaction?.GetDbTransaction();
    }


    public override async Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var savedEntity = (await dbContext.Set<TEntity>().AddAsync(entity, GetCancellationToken(cancellationToken))).Entity;

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
        }

        return savedEntity;
    }

    public override async Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var entityArray = entities.ToArray();
        if (entityArray.IsNullOrEmptySet())
        {
            return;
        }

        var dbContext = await GetDbContextAsync();
        cancellationToken = GetCancellationToken(cancellationToken);

        //if (BulkOperationProvider != null)
        //{
        //    await BulkOperationProvider.InsertManyAsync<TDbContext, TEntity>(
        //        this,
        //        entityArray,
        //        autoSave,
        //        GetCancellationToken(cancellationToken)
        //    );
        //    return;
        //}

        await dbContext.Set<TEntity>().AddRangeAsync(entityArray, cancellationToken);

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public override async Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        //dbContext.Set<TEntity>().Attach(entity);
        dbContext.Update(entity);
        //if (dbContext.Set<TEntity>().Local.All(e => e != entity))
        //{

        //}

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
        }

        return entity;
    }

    public override async Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var entityArray = entities.ToArray();
        if (entityArray.IsNullOrEmptySet())
        {
            return;
        }

        cancellationToken = GetCancellationToken(cancellationToken);

        //if (BulkOperationProvider != null)
        //{
        //    await BulkOperationProvider.UpdateManyAsync<TDbContext, TEntity>(
        //        this,
        //        entityArray,
        //        autoSave,
        //        GetCancellationToken(cancellationToken)
        //        );

        //    return;
        //}

        var dbContext = await GetDbContextAsync();

        dbContext.Set<TEntity>().UpdateRange(entityArray);

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public override async Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        dbContext.Set<TEntity>().Remove(entity);

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
        }
    }

    // Pitfall: ChangeTracker only refreshes entity states to Modified when ChangeTracker.Entries() is called
    // (internally invoking ChangeTracker.DetectChanges). If no value changes are detected, the state remains Unchanged.
    // In data synchronization scenarios, Delete operations may therefore not trigger an update as expected.
    public override async Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var entityArray = entities.ToArray();
        if (entityArray.IsNullOrEmptySet())
        {
            return;
        }

        //cancellationToken = GetCancellationToken(cancellationToken);

        //if (BulkOperationProvider != null)
        //{
        //    await BulkOperationProvider.DeleteManyAsync<TDbContext, TEntity>(
        //        this,
        //        entityArray,
        //        autoSave,
        //        cancellationToken
        //    );

        //    return;
        //}

        var dbContext = await GetDbContextAsync();

        dbContext.RemoveRange(entityArray.Select(x => x));

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public override async Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
    {
        return includeDetails
            ? await (await WithDetailsAsync()).ToListAsync(GetCancellationToken(cancellationToken))
            : await (await GetQueryableAsync()).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public override async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default)
    {
        return includeDetails
            ? await (await WithDetailsAsync()).Where(predicate).ToListAsync(GetCancellationToken(cancellationToken))
            : await (await GetQueryableAsync()).Where(predicate).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public override async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync()).LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public override async Task<IQueryable<TEntity>> GetQueryableAsync()
    {
        var queryable = (await GetDbSetAsync()).AsQueryable();
        return queryable;
    }

    // TODO: Evaluate whether this can be optimized to FirstOrDefault.
    public override async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        return includeDetails
            ? await (await WithDetailsAsync())
                .Where(predicate)
                .SingleOrDefaultAsync(GetCancellationToken(cancellationToken))
            : await (await GetQueryableAsync())
                .Where(predicate)
                .SingleOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public override async Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var dbSet = dbContext.Set<TEntity>();

        var entities = await dbSet
            .Where(predicate)
            .ToListAsync(GetCancellationToken(cancellationToken));

        await DeleteManyAsync(entities, autoSave, cancellationToken);

        if (autoSave)
        {
            await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
        }
    }

    public override async Task DeleteDirectAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var dbSet = dbContext.Set<TEntity>();
        await dbSet.Where(predicate).ExecuteDeleteAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task EnsureCollectionLoadedAsync<TProperty>(TEntity entity,
        Expression<Func<TEntity, IEnumerable<TProperty>>> propertyExpression,
        CancellationToken cancellationToken)
        where TProperty : class
    {
        await (await GetDbContextAsync())
            .Entry(entity)
            .Collection(propertyExpression)
            .LoadAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task EnsurePropertyLoadedAsync<TProperty>(
        TEntity entity,
        Expression<Func<TEntity, TProperty?>> propertyExpression,
        CancellationToken cancellationToken = default)
        where TProperty : class
    {
        await (await GetDbContextAsync())
            .Entry(entity)
            .Reference(propertyExpression)
            .LoadAsync(GetCancellationToken(cancellationToken));
    }

    public override async Task<IQueryable<TEntity>> WithDetailsAsync(params Expression<Func<TEntity, object>>[] propertySelectors)
    {
        return IncludeDetails(
            await GetQueryableAsync(),
            propertySelectors
        );
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

    public IQueryable<TEntity> DisableSoftDeleteFilter(IQueryable<TEntity> queryable)
    {
        //TODO https://github.com/dotnet/efcore/issues/17347
        return queryable.IgnoreQueryFilters();
    }
}

public class EfRepository<TDbContext, TEntity, TKey>(IDbContextProvider<TDbContext> dbContextProvider) : EfRepository<TDbContext, TEntity>(dbContextProvider), IRepository<TEntity, TKey>
    where TDbContext : RepositoryDbContext<TDbContext>
    where TEntity : class, IEntity<TKey>
{
    public virtual async Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, includeDetails, GetCancellationToken(cancellationToken));

        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity), id);
        }

        return entity;
    }

    public virtual async Task<TEntity?> FindAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default)
    {
        return includeDetails
            ? await (await WithDetailsAsync()).OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.Id!.Equals(id), GetCancellationToken(cancellationToken))
            : await (await GetQueryableAsync()).OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.Id!.Equals(id), GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> ExistAsync(TKey id)
    {
        return (await GetQueryableAsync()).Any(s => s.Id!.Equals(id));
    }

    public virtual async Task DeleteAsync(TKey id, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, cancellationToken: cancellationToken);
        if (entity == null)
        {
            return;
        }

        await DeleteAsync(entity, autoSave, cancellationToken);
    }

    public virtual async Task DeleteManyAsync(IEnumerable<TKey> ids, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        cancellationToken = GetCancellationToken(cancellationToken);

        var entities = await (await GetDbSetAsync()).Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

        await DeleteManyAsync(entities, autoSave, cancellationToken);
    }
}
