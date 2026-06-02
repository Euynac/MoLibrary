using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Repository.Persistence.Extensions;

/// <summary>
/// Provides explicit loading helpers for repository-managed entities.
/// </summary>
public static class RepositoryLoadExtensions
{
    /// <summary>
    /// Explicitly loads a collection navigation for an entity using the repository DbContext.
    /// </summary>
    public static async Task LoadCollectionAsync<TEntity, TProperty>(
        this IRepository<TEntity> repository,
        TEntity entity,
        Expression<Func<TEntity, IEnumerable<TProperty>>> selector,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
        where TProperty : class
    {
        await (await repository.GetDbContextAsync())
            .Entry(entity)
            .Collection(selector)
            .LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Explicitly loads a reference navigation for an entity using the repository DbContext.
    /// </summary>
    public static async Task LoadReferenceAsync<TEntity, TProperty>(
        this IRepository<TEntity> repository,
        TEntity entity,
        Expression<Func<TEntity, TProperty?>> selector,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
        where TProperty : class
    {
        await (await repository.GetDbContextAsync())
            .Entry(entity)
            .Reference(selector)
            .LoadAsync(cancellationToken);
    }
}
