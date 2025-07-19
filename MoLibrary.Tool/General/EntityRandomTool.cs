using System.Linq;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// Random Tool for entity or linq.
    /// </summary>
    public static class EntityRandomTool
    {
        /// <summary>
        /// Get a random item from the <see cref="IQueryable&lt;T&gt;"/>.
        /// </summary>
        /// <param name="queryable"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns><c>default</c>(<paramref name="T" />) if list is empty; otherwise, the first element in result list.</returns>
        public static T? RandomGetOne<T>(this IQueryable<T> queryable)
        {
            // //OrderBy Guid.NewGuid() does not work when using Include with Many to Many? #5583
            // return queryable.OrderBy(r => Guid.NewGuid()).FirstOrDefault();
            var total = queryable.Count();
            if (total == 0) return default;
            var offset = RandomTool.GetInt(0, total - 1);
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
}