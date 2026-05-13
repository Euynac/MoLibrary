using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Exceptions;

namespace Monica.Repository.Persistence.Services;

/// <summary>
/// Provides shared repository behavior that is independent of a concrete database provider.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
public abstract class RepositoryBase<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public abstract IRepositoryQuery<TEntity> Query();

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
    public abstract Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(predicate, cancellationToken);

        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity));
        }

        return entity;
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
