using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Exceptions;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.Persistence.Services;

/// <summary>
/// Entity Framework Core repository implementation.
/// </summary>
public class EfRepository<TDbContext, TEntity>(
    IDbContextProvider<TDbContext> dbContextProvider)
    : RepositoryBase<TEntity>
    where TDbContext : RepositoryDbContext<TDbContext>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public override IRepositoryQuery<TEntity> Query()
    {
        return new EfRepositoryQuery<TEntity>(CreateBaseQueryAsync, this);
    }

    /// <inheritdoc />
    public override async Task<DbContext> GetDbContextAsync()
    {
        return await GetTypedDbContextAsync();
    }

    protected virtual Task<TDbContext> GetTypedDbContextAsync()
    {
        return dbContextProvider.GetDbContextAsync();
    }

    /// <inheritdoc />
    public override async Task<DbSet<TEntity>> GetDbSetAsync()
    {
        return (await GetTypedDbContextAsync()).Set<TEntity>();
    }

    protected async Task<IDbConnection> GetDbConnectionAsync()
    {
        return (await GetTypedDbContextAsync()).Database.GetDbConnection();
    }

    protected async Task<IDbTransaction?> GetDbTransactionAsync()
    {
        return (await GetTypedDbContextAsync()).Database.CurrentTransaction?.GetDbTransaction();
    }

    /// <inheritdoc />
    public override async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetTypedDbContextAsync();
        return (await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken)).Entity;
    }

    /// <inheritdoc />
    public override async Task InsertManyAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        await (await GetDbSetAsync()).AddRangeAsync(entityArray, cancellationToken);
    }

    /// <inheritdoc />
    public override void Attach(TEntity entity)
    {
        GetTypedDbContextAsync().GetAwaiter().GetResult().Set<TEntity>().Attach(entity);
    }

    /// <inheritdoc />
    public override void Update(TEntity entity)
    {
        GetTypedDbContextAsync().GetAwaiter().GetResult().Update(entity);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task DeleteManyAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        var dbContext = await GetTypedDbContextAsync();
        dbContext.RemoveRange(entityArray);
    }

    /// <inheritdoc />
    public override async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await Query().SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await Query()
            .Where(predicate)
            .ToListAsync(cancellationToken);

        await DeleteManyAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<UpdateSettersBuilder<TEntity>> setters,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(predicate)
            .ExecuteUpdateAsync(setters, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(predicate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = await GetTypedDbContextAsync();
        var current = dbContext.CachedServiceProvider.GetService<IUnitOfWorkManager>()?.Current;
        if (current is { IsCompleted: false } unitOfWork &&
            current is IUnitOfWorkInternals internals &&
            internals.TryGetDbContext<TDbContext>() != null)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return 0;
        }

        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IQueryable<TEntity>> CreateBaseQueryAsync()
    {
        return (await GetDbSetAsync()).AsQueryable();
    }
}

/// <summary>
/// Entity Framework Core repository implementation for entities with a single primary key.
/// </summary>
public class EfRepository<TDbContext, TEntity, TKey>(
    IDbContextProvider<TDbContext> dbContextProvider)
    : EfRepository<TDbContext, TEntity>(dbContextProvider), IRepository<TEntity, TKey>
    where TDbContext : RepositoryDbContext<TDbContext>
    where TEntity : class, IEntity<TKey>
{
    /// <inheritdoc />
    public virtual async Task<TEntity> GetAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, cancellationToken);

        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity), id);
        }

        return entity;
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Query()
            .OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(entity => entity.Id!.Equals(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Query().AnyAsync(entity => entity.Id!.Equals(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        await DeleteAsync(entity, cancellationToken);
    }
}
