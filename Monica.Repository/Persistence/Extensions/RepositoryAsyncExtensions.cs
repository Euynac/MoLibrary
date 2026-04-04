using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Repository.Persistence.Extensions;

public static class RepositoryAsyncExtensions
{
    #region Contains

    public static async Task<bool> ContainsAsync<T>(
        this IBasicRepository<T> repository,
        T item,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.ContainsAsync(item, cancellationToken);
    }

    #endregion

    #region Any/All

    public static async Task<bool> AnyAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AnyAsync(cancellationToken);
    }

    public static async Task<bool> AnyAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AnyAsync(predicate, cancellationToken);
    }

    public static async Task<bool> AllAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AllAsync(predicate, cancellationToken);
    }

    #endregion

    #region Count/LongCount

    public static async Task<int> CountAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.CountAsync(cancellationToken);
    }

    public static async Task<int> CountAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.CountAsync(predicate, cancellationToken);
    }

    public static async Task<long> LongCountAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LongCountAsync(cancellationToken);
    }

    public static async Task<long> LongCountAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LongCountAsync(predicate, cancellationToken);
    }

    #endregion

    #region First/FirstOrDefault

    public static async Task<T> FirstAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstAsync(cancellationToken);
    }

    public static async Task<T> FirstAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstAsync(predicate, cancellationToken);
    }

    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region Last/LastOrDefault

    public static async Task<T> LastAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastAsync(cancellationToken);
    }

    public static async Task<T> LastAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastAsync(predicate, cancellationToken);
    }

    public static async Task<T?> LastOrDefaultAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastOrDefaultAsync(cancellationToken);
    }

    public static async Task<T?> LastOrDefaultAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region Single/SingleOrDefault

    public static async Task<T> SingleAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleAsync(cancellationToken);
    }

    public static async Task<T> SingleAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleAsync(predicate, cancellationToken);
    }

    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleOrDefaultAsync(cancellationToken);
    }

    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region Min

    public static async Task<T> MinAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MinAsync(cancellationToken);
    }

    public static async Task<TResult> MinAsync<T, TResult>(
        this IBasicRepository<T> repository,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MinAsync(selector, cancellationToken);
    }

    #endregion

    #region Max

    public static async Task<T> MaxAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MaxAsync(cancellationToken);
    }

    public static async Task<TResult> MaxAsync<T, TResult>(
        this IBasicRepository<T> repository,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MaxAsync(selector, cancellationToken);
    }

    //Todo will be optimized after .NET supports MaxBy.
    public static async Task<T?> MaxByAsync<T, TKey>(
        this IBasicRepository<T> repository,
        Expression<Func<T, TKey>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.OrderByDescending(selector).FirstOrDefaultAsync(cancellationToken);
    }
    #endregion

    #region Sum

    public static async Task<decimal> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, decimal>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<decimal?> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, decimal?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<int> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, int>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<int?> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, int?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<long> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, long>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<long?> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, long?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<double> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, double>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<double?> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, double?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<float> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, float>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<float?> SumAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, float?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    #endregion

    #region Average

    public static async Task<decimal> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, decimal>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<decimal?> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, decimal?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, int>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double?> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, int?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, long>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double?> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, long?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, double>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double?> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, double?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<float?> AverageAsync<T>(
        this IBasicRepository<T> repository,
        Expression<Func<T, float?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    #endregion

    #region ToList/Array

    public static async Task<List<T>> ToListAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.ToListAsync(cancellationToken);
    }

    public static async Task<T[]> ToArrayAsync<T>(
        this IBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.ToArrayAsync(cancellationToken);
    }

    #endregion
}
