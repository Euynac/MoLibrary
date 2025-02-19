using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocksPlatform.Features.EfCoreExtensions;


public static class EfCoreExclude
{
    internal const string EXCLUDE_PROPERTY_ANNOTATION = "Excluded property:";
    /// <summary>
    /// 排除字段不查询 (目前仅支持PgSQL，以及string类型忽略，请不要到处使用)
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="query">目前仅支持单个属性，不支持匿名</param>
    /// <param name="propertyPath"></param>
    /// <returns></returns>
    public static IQueryable<TEntity> Exclude<TEntity, TProperty>(this IQueryable<TEntity> query,
        Expression<Func<TEntity, TProperty>> propertyPath) where TEntity : class
    {
        var exp = propertyPath;
        var sb = new StringBuilder();
        if (propertyPath.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException(nameof(propertyPath));
        }

        sb.Append(memberExpression.Member.Name);
        return query.TagWith(EXCLUDE_PROPERTY_ANNOTATION + sb);
    }
}

public class RepositoryTaggedQueryCommandInterceptor : DbCommandInterceptor
{
   
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        ManipulateCommand(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ManipulateCommand(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void ManipulateCommand(DbCommand command)
    {
        var sql = command.CommandText;
        var finalSql = "";
        if (!sql.StartsWith('-')) return;
        //exclude property
        var excludePropertyKey = $"-- {EfCoreExclude.EXCLUDE_PROPERTY_ANNOTATION}";
        if (sql.StartsWith(excludePropertyKey, StringComparison.Ordinal))
        {
            var endIndex = sql.IndexOfAny(['\n', '\r']);
            var excluded = sql[excludePropertyKey.Length..endIndex];
            
            //发现这种执行sql效率还不如直接取，是因为会使得sql变成O(n)?
            finalSql = sql.Replace($"""
                                    f."{excluded}"
                                    """, $"""
                                          '' AS "{excluded}"
                                          """);
        }

        if (!string.IsNullOrEmpty(finalSql))
        {
            command.CommandText = finalSql;
        }
    }
}