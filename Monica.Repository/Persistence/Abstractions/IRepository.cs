using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.Entity.Abstractions;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Defines the repository contract for managing entities of type <typeparamref name="TEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
/// <remarks>
/// Repository write methods stage changes on the underlying DbContext. Call <see cref="SaveChangesAsync"/>
/// to flush changes explicitly, or rely on an active unit of work to flush and commit at completion.
/// </remarks>
public interface IRepository<TEntity> : IRepositoryFeatures, ITransientDependency
    where TEntity : class, IEntity
{
    /// <summary>
    /// Creates a deferred query builder for this repository.
    /// </summary>
    IRepositoryQuery<TEntity> Query();

    /// <summary>
    /// Finds a single entity matching the predicate.
    /// </summary>
    /// <returns>The matching entity, or <see langword="null"/> when no entity matches.</returns>
    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single entity matching the predicate.
    /// </summary>
    /// <exception cref="Persistence.Exceptions.EntityNotFoundException">Thrown when no entity matches.</exception>
    Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new entity for insertion.
    /// </summary>
    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages multiple new entities for insertion.
    /// </summary>
    Task InsertManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an existing entity as unchanged.
    /// </summary>
    void Attach(TEntity entity);

    /// <summary>
    /// Marks a detached entity root as modified.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Stages an entity for deletion.
    /// </summary>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages multiple entities for deletion.
    /// </summary>
    Task DeleteManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads entities matching the predicate and stages them for deletion.
    /// </summary>
    Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a set-based update immediately against the database.
    /// </summary>
    /// <remarks>
    /// EF Core executes this operation immediately and does not update tracked entity instances.
    /// When a unit of work transaction is active, the operation participates in that transaction.
    /// </remarks>
    Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<UpdateSettersBuilder<TEntity>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a set-based delete immediately against the database.
    /// </summary>
    /// <remarks>
    /// EF Core executes this operation immediately. Use <see cref="DeleteAsync(Expression{Func{TEntity, bool}}, CancellationToken)"/>
    /// when soft-delete, auditing, and entity change events must run through tracked entities.
    /// </remarks>
    Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the DbContext associated with this repository.
    /// </summary>
    Task<DbContext> GetDbContextAsync();

    /// <summary>
    /// Gets the DbSet associated with this repository.
    /// </summary>
    Task<DbSet<TEntity>> GetDbSetAsync();

    /// <summary>
    /// Flushes staged changes.
    /// </summary>
    /// <remarks>
    /// Inside an active unit of work this delegates to the unit of work and does not commit the transaction.
    /// Outside a unit of work this saves the repository DbContext directly.
    /// </remarks>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines keyed repository operations for entities with a single primary key.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
/// <typeparam name="TKey">The entity key type.</typeparam>
public interface IRepository<TEntity, in TKey> : IRepository<TEntity>
    where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// Finds an entity by primary key.
    /// </summary>
    Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by primary key.
    /// </summary>
    /// <exception cref="Persistence.Exceptions.EntityNotFoundException">Thrown when the entity does not exist.</exception>
    Task<TEntity> GetAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an entity with the primary key exists.
    /// </summary>
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by primary key if it exists.
    /// </summary>
    Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository feature flags used by framework components.
/// </summary>
public interface IRepositoryFeatures
{
    /// <summary>
    /// Returns whether the repository stores data in sharded tables where query ordering may need special handling.
    /// </summary>
    bool IsShardingTable() => false;
}
