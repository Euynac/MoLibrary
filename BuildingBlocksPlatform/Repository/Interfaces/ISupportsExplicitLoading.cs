using System.Linq.Expressions;
using BuildingBlocksPlatform.Repository.EntityInterfaces;

namespace BuildingBlocksPlatform.Repository.Interfaces;

public interface ISupportsExplicitLoading<TEntity>
    where TEntity : class, IMoEntity
{
    Task EnsureCollectionLoadedAsync<TProperty>(TEntity entity,
        Expression<Func<TEntity, IEnumerable<TProperty>>> propertyExpression,
        CancellationToken cancellationToken = default)
        where TProperty : class;

    Task EnsurePropertyLoadedAsync<TProperty>(
        TEntity entity,
        Expression<Func<TEntity, TProperty?>> propertyExpression,
        CancellationToken cancellationToken = default)
        where TProperty : class;
}
