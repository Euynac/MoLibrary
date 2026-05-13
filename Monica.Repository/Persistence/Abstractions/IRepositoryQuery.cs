using System.Linq.Expressions;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Represents a deferred repository query that can be composed synchronously and executed asynchronously.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
/// <remarks>
/// Implementations should avoid resolving the underlying database context until a terminal asynchronous method is called.
/// This keeps query composition cheap while preserving scoped DbContext ownership.
/// </remarks>
public interface IRepositoryQuery<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Adds a filter predicate to the query.
    /// </summary>
    IRepositoryQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// Includes a navigation property by expression.
    /// </summary>
    IRepositoryQuery<TEntity> Include(Expression<Func<TEntity, object?>> selector);

    /// <summary>
    /// Includes a navigation property by a dot-separated navigation path.
    /// </summary>
    IRepositoryQuery<TEntity> Include(string navigationPath);

    /// <summary>
    /// Applies the repository's default detail configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the repository does not implement <see cref="IRepositoryDetailsConfigurator{TEntity}"/>.
    /// </exception>
    IRepositoryQuery<TEntity> WithDetails();

    /// <summary>
    /// Configures the query to return entities without change tracking.
    /// </summary>
    IRepositoryQuery<TEntity> AsNoTracking();

    /// <summary>
    /// Configures the query to return tracked entities.
    /// </summary>
    IRepositoryQuery<TEntity> AsTracking();

    /// <summary>
    /// Ignores the soft-delete query filter for this query.
    /// </summary>
    IRepositoryQuery<TEntity> IgnoreSoftDeleteFilter();

    /// <summary>
    /// Ignores all EF Core query filters for this query.
    /// </summary>
    IRepositoryQuery<TEntity> IgnoreQueryFilters();

    /// <summary>
    /// Orders the query by the selected key.
    /// </summary>
    IRepositoryQuery<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <summary>
    /// Orders the query by the selected key in descending order.
    /// </summary>
    IRepositoryQuery<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    IRepositoryQuery<TEntity> Skip(int count);

    /// <summary>
    /// Takes the specified number of rows.
    /// </summary>
    IRepositoryQuery<TEntity> Take(int count);

    /// <summary>
    /// Materializes the query as a list.
    /// </summary>
    Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Materializes the query as an array.
    /// </summary>
    Task<TEntity[]> ToArrayAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first entity or <see langword="null"/> when the query is empty.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a predicate and returns the first matching entity, or <see langword="null"/> when none exists.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first entity and lets EF Core throw when the query is empty.
    /// </summary>
    Task<TEntity> FirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a predicate and returns the first matching entity.
    /// </summary>
    Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single matching entity or <see langword="null"/> when none exists.
    /// </summary>
    Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the query contains any rows.
    /// </summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether any rows match the predicate.
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of rows as an <see cref="int"/>.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of rows as a <see cref="long"/>.
    /// </summary>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Materializes a one-based page and total count from the current query.
    /// </summary>
    Task<PagedList<TEntity>> ToPagedListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exposes the composed queryable for advanced LINQ operations that are not part of the repository query contract.
    /// </summary>
    /// <remarks>
    /// The returned query is bound to the DbContext resolved for this repository. The caller must enumerate it within that scope.
    /// </remarks>
    Task<IQueryable<TEntity>> AsQueryableAsync();
}
