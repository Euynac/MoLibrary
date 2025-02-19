using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Diagnostics.CodeAnalysis;
using BuildingBlocksPlatform.Repository.EntityInterfaces;


namespace BuildingBlocksPlatform.Extensions;

public static class DbContextExtensions
{
    /// <summary>
    /// 统一配置实体
    /// </summary>
    /// <param name="builder"></param>
    public static void UnifiedConfigEntity(this EntityTypeBuilder builder)
    {
        builder.ConfigureByConvention();
    }
    /// <summary>
    /// 判断IQueryable是否已被OrderBy或OrderByDescending过
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