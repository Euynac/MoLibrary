using Microsoft.EntityFrameworkCore;
using Monica.Repository.Entity.Abstractions;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Provides the default eager-loading shape used by <see cref="IRepositoryRead{TEntity}.WithDetails"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type whose details are configured.</typeparam>
/// <remarks>
/// Implement this interface on a repository when the entity has a canonical detail graph.
/// Use EF Core <see cref="EntityFrameworkQueryableExtensions.Include{TEntity, TProperty}(IQueryable{TEntity}, System.Linq.Expressions.Expression{Func{TEntity, TProperty}})"/>
/// and related query operators inside <see cref="ApplyDetails"/>.
/// </remarks>
public interface IRepositoryDetailsConfigurator<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Applies the repository's default eager-loading graph to the query.
    /// </summary>
    /// <param name="query">The query to extend.</param>
    /// <returns>The query with default detail includes applied.</returns>
    IQueryable<TEntity> ApplyDetails(IQueryable<TEntity> query);
}
