using System.Linq.Expressions;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Represents the read side of a repository.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
/// <remarks>
/// Reads are no-tracking by default. Call <see cref="AsTracking"/> before materialization when the returned
/// entities must participate in EF Core change tracking.
/// </remarks>
public interface IRepositoryRead<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Returns a read scope that materializes tracked entities.
    /// </summary>
    IRepositoryRead<TEntity> AsTracking();

    /// <summary>
    /// Returns a read scope that materializes entities without change tracking.
    /// </summary>
    IRepositoryRead<TEntity> AsNoTracking();

    /// <summary>
    /// Adds a filter predicate to the read scope.
    /// </summary>
    IRepositoryRead<TEntity> Where(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// Includes a navigation property by expression.
    /// </summary>
    IRepositoryRead<TEntity> Include(Expression<Func<TEntity, object?>> selector);

    /// <summary>
    /// Includes a navigation property by a dot-separated navigation path.
    /// </summary>
    IRepositoryRead<TEntity> Include(string navigationPath);

    /// <summary>
    /// Applies the repository's default detail configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the repository does not implement <see cref="IRepositoryDetailsConfigurator{TEntity}"/>.
    /// </exception>
    IRepositoryRead<TEntity> WithDetails();

    /// <summary>
    /// Ignores the soft-delete query filter for this read scope.
    /// </summary>
    IRepositoryRead<TEntity> IgnoreSoftDeleteFilter();

    /// <summary>
    /// Ignores all EF Core query filters for this read scope.
    /// </summary>
    IRepositoryRead<TEntity> IgnoreQueryFilters();

    /// <summary>
    /// Orders the read scope by the selected key.
    /// </summary>
    IRepositoryRead<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <summary>
    /// Orders the read scope by the selected key in descending order.
    /// </summary>
    IRepositoryRead<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <summary>
    /// Adds a secondary ascending order to a read scope that already has a primary order.
    /// </summary>
    IRepositoryRead<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <summary>
    /// Adds a secondary descending order to a read scope that already has a primary order.
    /// </summary>
    IRepositoryRead<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> selector);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    IRepositoryRead<TEntity> Skip(int count);

    /// <summary>
    /// Takes the specified number of rows.
    /// </summary>
    IRepositoryRead<TEntity> Take(int count);

    /// <summary>
    /// Materializes the current read scope as a list.
    /// </summary>
    Task<List<TEntity>> GetListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a predicate and materializes the matching entities as a list.
    /// </summary>
    Task<List<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

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
    /// Returns the first entity or <see langword="null"/> when the read scope is empty.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a predicate and returns the first matching entity, or <see langword="null"/> when none exists.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first entity and lets EF Core throw when the read scope is empty.
    /// </summary>
    Task<TEntity> FirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a predicate and returns the first matching entity.
    /// </summary>
    Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single matching entity or <see langword="null"/> when none exists.
    /// </summary>
    Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the read scope contains any rows.
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
    /// Returns the number of rows that match the predicate as an <see cref="int"/>.
    /// </summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of rows as a <see cref="long"/>.
    /// </summary>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of rows that match the predicate as a <see cref="long"/>.
    /// </summary>
    Task<long> LongCountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Materializes a one-based page and total count from the current read scope.
    /// </summary>
    Task<PagedList<TEntity>> GetPagedListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a predicate and materializes a one-based page and total count.
    /// </summary>
    Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exposes the composed queryable for advanced LINQ operations that are not covered by repository terminal methods.
    /// </summary>
    /// <remarks>
    /// The returned query is bound to the DbContext resolved for this repository. Enumerate it within the active DI or
    /// unit-of-work scope. The query follows the current tracking mode.
    /// </remarks>
    Task<IQueryable<TEntity>> GetQueryableAsync();
}
