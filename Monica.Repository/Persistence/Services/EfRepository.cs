using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Exceptions;
using Monica.Repository.Persistence.Models;
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
    public override IRepositoryRead<TEntity> AsTracking()
    {
        return CreateReadScope().AsTracking();
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> AsNoTracking()
    {
        return CreateReadScope().AsNoTracking();
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        return CreateReadScope().Where(predicate);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> Include(Expression<Func<TEntity, object?>> selector)
    {
        return CreateReadScope().Include(selector);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> Include(string navigationPath)
    {
        return CreateReadScope().Include(navigationPath);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> WithDetails()
    {
        return CreateReadScope().WithDetails();
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> IgnoreSoftDeleteFilter()
    {
        return CreateReadScope().IgnoreSoftDeleteFilter();
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> IgnoreQueryFilters()
    {
        return CreateReadScope().IgnoreQueryFilters();
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return CreateReadScope().OrderBy(selector);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return CreateReadScope().OrderByDescending(selector);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return CreateReadScope().ThenBy(selector);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return CreateReadScope().ThenByDescending(selector);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> Skip(int count)
    {
        return CreateReadScope().Skip(count);
    }

    /// <inheritdoc />
    public override IRepositoryRead<TEntity> Take(int count)
    {
        return CreateReadScope().Take(count);
    }

    /// <inheritdoc />
    public override async Task<List<TEntity>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().GetListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<List<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().GetListAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().FindAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().GetAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity> FirstAsync(CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().FirstAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().FirstAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().CountAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().LongCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<long> LongCountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().LongCountAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().GetPagedListAsync(page, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await CreateReadScope().GetPagedListAsync(page, pageSize, predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<IQueryable<TEntity>> GetQueryableAsync()
    {
        return CreateReadScope().GetQueryableAsync();
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
    public override async Task AttachAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (await GetTypedDbContextAsync()).Set<TEntity>().Attach(entity);
    }

    /// <inheritdoc />
    public override async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (await GetTypedDbContextAsync()).Update(entity);
    }

    /// <inheritdoc />
    public override async Task UpdateManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (await GetTypedDbContextAsync()).Set<TEntity>().UpdateRange(entities);
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
    public override async Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await AsTracking().GetListAsync(predicate, cancellationToken);

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

    private IRepositoryRead<TEntity> CreateReadScope()
    {
        return new EfRepositoryRead<TEntity>(CreateBaseQueryAsync, this);
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
        return await OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(entity => entity.Id!.Equals(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await AnyAsync(entity => entity.Id!.Equals(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await AsTracking().FirstOrDefaultAsync(entity => entity.Id!.Equals(id), cancellationToken);
        if (entity == null)
        {
            return;
        }

        await DeleteAsync(entity, cancellationToken);
    }
}
