using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services;

/// <summary>
/// Provides shared repository behavior that is independent of a concrete database provider.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
public abstract class RepositoryBase<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> AsTracking();

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> AsNoTracking();

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> Where(Expression<Func<TEntity, bool>> predicate);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> Include(Expression<Func<TEntity, object?>> selector);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> Include(string navigationPath);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> WithDetails();

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> IgnoreSoftDeleteFilter();

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> IgnoreQueryFilters();

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> Skip(int count);

    /// <inheritdoc />
    public abstract IRepositoryRead<TEntity> Take(int count);

    /// <inheritdoc />
    public abstract Task<List<TEntity>> GetListAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<List<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity> FirstAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<long> LongCountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<IQueryable<TEntity>> GetQueryableAsync();

    /// <inheritdoc />
    public abstract Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task InsertManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await InsertAsync(entity, cancellationToken);
        }
    }

    /// <inheritdoc />
    public abstract void Attach(TEntity entity);

    /// <inheritdoc />
    public abstract void Update(TEntity entity);

    /// <inheritdoc />
    public abstract Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task DeleteManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    /// <inheritdoc />
    public abstract Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<DbContext> GetDbContextAsync();

    /// <inheritdoc />
    public abstract Task<DbSet<TEntity>> GetDbSetAsync();

    /// <inheritdoc />
    public abstract Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<UpdateSettersBuilder<TEntity>> setters,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<int> ExecuteDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
}
