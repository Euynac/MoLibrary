using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Monica.Repository.Persistence.Extensions;

/// <summary>
/// Tags a query so selected properties are replaced with empty values in the generated SQL projection.
/// </summary>
public static class QueryableExcludeExtensions
{
    internal const string ExcludedPropertyAnnotation = "Excluded property:";

    /// <summary>
    /// Excludes a projected property from the SQL payload while preserving the query shape.
    /// This is intended for PostgreSQL-style providers and should be used sparingly.
    /// </summary>
    public static IQueryable<TEntity> Exclude<TEntity, TProperty>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TProperty>> propertyPath)
        where TEntity : class
    {
        if (propertyPath.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException(nameof(propertyPath));
        }

        var builder = new StringBuilder();
        builder.Append(memberExpression.Member.Name);
        return query.TagWith(ExcludedPropertyAnnotation + builder);
    }
}
