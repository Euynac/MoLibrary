namespace Monica.Tool.General;

/// <summary>
/// Adds random-selection helpers for <see cref="IQueryable{T}"/>.
/// </summary>
public static class QueryableRandomExtensions
{
    /// <summary>
    /// Returns one random element from the queryable sequence.
    /// </summary>
    /// <param name="queryable"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><c>default</c> if list is empty; otherwise, the first element in result list.</returns>
    public static T? RandomGetOne<T>(this IQueryable<T> queryable)
    {
        // //OrderBy Guid.NewGuid() does not work when using Include with Many to Many? #5583
        // return queryable.OrderBy(r => Guid.NewGuid()).FirstOrDefault();
        var total = queryable.Count();
        if (total == 0) return default;
        var offset = RandomValueGenerator.NextInt(0, total - 1);
        return queryable.Skip(offset).FirstOrDefault();
    }

    // /// <summary>
    // /// Get a number of random items from <see cref="IQueryable&lt;T&gt;"/> (no duplicates).
    // /// </summary>
    // /// <param name="queryable"></param>
    // /// <param name="count"></param>
    // /// <typeparam name="T"></typeparam>
    // /// <returns></returns>
    // [NotNull]
    // public static IList<T> RandomGet<T>(this IQueryable<T> queryable, int count)
    // {
    //     return queryable.OrderBy(r => Guid.NewGuid()).Take(count).ToList();
    // }
}
