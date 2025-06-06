using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace MoLibrary.Repository.Extensions;

public static class EntityTypeBuilderExtensions
{
    /// <summary>
    /// This method is used to add a query filter to this entity which combine with Mo EF Core builtin query filters.
    /// </summary>
    /// <returns></returns>
    public static EntityTypeBuilder<TEntity> HasMoQueryFilter<TEntity>(this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, bool>> filter)
        where TEntity : class
    {
#pragma warning disable EF1001
        var queryFilterAnnotation = builder.Metadata.FindAnnotation(CoreAnnotationNames.QueryFilter);
#pragma warning restore EF1001
        if (queryFilterAnnotation is { Value: Expression<Func<TEntity, bool>> existingFilter })
        {
            filter = QueryFilterExpressionHelper.CombineExpressions(filter, existingFilter);
        }

        return builder.HasQueryFilter(filter);
    }
}
