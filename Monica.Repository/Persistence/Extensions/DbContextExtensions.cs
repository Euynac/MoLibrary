using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Monica.Repository.Entity.Extensions;

namespace Monica.Repository.Persistence.Extensions;

public static class DbContextExtensions
{
    /// <summary>
    /// Unified configuration entities
    /// </summary>
    /// <param name="builder"></param>
    public static void UnifiedConfigEntity(this EntityTypeBuilder builder)
    {
        builder.ConfigureByConvention();
    }
    /// <summary>
    /// Determine whether IQueryable has been OrderBy or OrderByDescending
    /// </summary>
    /// <param name="query"></param>
    /// <param name="orderedQueryable"></param>
    /// <returns></returns>
    public static bool HasBeenOrdered<TEntity>(this IQueryable<TEntity> query, [NotNullWhen(true)] out IOrderedQueryable<TEntity>? orderedQueryable)
    {
        orderedQueryable = null;
        if (query.Expression.Type == typeof(IOrderedQueryable<TEntity>) && query is IOrderedQueryable<TEntity> ordered)
        {
            orderedQueryable = ordered;
            return true;
        }

        return false;
    }

    public static bool HasBeenEnumerable<TEntity>(this IQueryable<TEntity> query,
        [NotNullWhen(true)] out IEnumerable<TEntity>? enumerable)
    {
        enumerable = null;
        if (query.Provider is not EntityQueryProvider && query is IEnumerable<TEntity> entities)
        {
            enumerable = entities;
            return true;
        }

        return false;

        //if (query.Expression.Type == typeof(EnumerableQuery<TEntity>) && query is IEnumerable<TEntity> entities)
        //{
        //    enumerable = entities;
        //    return true;
        //}
        //return false;
    }


}