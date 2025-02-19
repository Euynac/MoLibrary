using System.Linq.Expressions;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocksPlatform.Repository.Extensions;

public static class RepositoryAsyncExtensions
{
    #region Contains

    public static async Task<bool> ContainsAsync<T>(
        this IMoBasicRepository<T> repository,
        T item,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.ContainsAsync(item, cancellationToken);
    }

    #endregion

    #region Any/All

    public static async Task<bool> AnyAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AnyAsync(cancellationToken);
    }

    public static async Task<bool> AnyAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AnyAsync(predicate, cancellationToken);
    }

    public static async Task<bool> AllAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AllAsync(predicate, cancellationToken);
    }

    #endregion

    #region Count/LongCount

    public static async Task<int> CountAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.CountAsync(cancellationToken);
    }

    public static async Task<int> CountAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.CountAsync(predicate, cancellationToken);
    }

    public static async Task<long> LongCountAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LongCountAsync(cancellationToken);
    }

    public static async Task<long> LongCountAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LongCountAsync(predicate, cancellationToken);
    }

    #endregion

    #region First/FirstOrDefault

    public static async Task<T> FirstAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstAsync(cancellationToken);
    }

    public static async Task<T> FirstAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstAsync(predicate, cancellationToken);
    }

    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region Last/LastOrDefault

    public static async Task<T> LastAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastAsync(cancellationToken);
    }

    public static async Task<T> LastAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastAsync(predicate, cancellationToken);
    }

    public static async Task<T?> LastOrDefaultAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastOrDefaultAsync(cancellationToken);
    }

    public static async Task<T?> LastOrDefaultAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.LastOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region Single/SingleOrDefault

    public static async Task<T> SingleAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleAsync(cancellationToken);
    }

    public static async Task<T> SingleAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleAsync(predicate, cancellationToken);
    }

    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleOrDefaultAsync(cancellationToken);
    }

    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region Min

    public static async Task<T> MinAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MinAsync(cancellationToken);
    }

    public static async Task<TResult> MinAsync<T, TResult>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MinAsync(selector, cancellationToken);
    }

    #endregion

    #region Max

    public static async Task<T> MaxAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MaxAsync(cancellationToken);
    }

    public static async Task<TResult> MaxAsync<T, TResult>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.MaxAsync(selector, cancellationToken);
    }

    #endregion

    #region Sum

    public static async Task<decimal> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, decimal>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<decimal?> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, decimal?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<int> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, int>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<int?> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, int?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<long> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, long>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<long?> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, long?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<double> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, double>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<double?> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, double?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<float> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, float>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    public static async Task<float?> SumAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, float?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.SumAsync(selector, cancellationToken);
    }

    #endregion

    #region Average

    public static async Task<decimal> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, decimal>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<decimal?> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, decimal?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, int>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double?> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, int?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, long>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double?> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, long?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, double>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<double?> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, double?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    public static async Task<float?> AverageAsync<T>(
        this IMoBasicRepository<T> repository,
        Expression<Func<T, float?>> selector,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.AverageAsync(selector, cancellationToken);
    }

    #endregion

    #region ToList/Array

    public static async Task<List<T>> ToListAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.ToListAsync(cancellationToken);
    }

    public static async Task<T[]> ToArrayAsync<T>(
        this IMoBasicRepository<T> repository,
        CancellationToken cancellationToken = default)
        where T : class, IMoEntity
    {
        var queryable = await repository.GetQueryableAsync();
        return await queryable.ToArrayAsync(cancellationToken);
    }

    #endregion
}
