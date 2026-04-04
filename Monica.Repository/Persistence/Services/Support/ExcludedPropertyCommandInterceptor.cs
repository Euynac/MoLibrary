using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Monica.Repository.Persistence.Extensions;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Rewrites tagged SQL queries so selected columns are excluded from the returned payload.
/// </summary>
public class ExcludedPropertyCommandInterceptor : DbCommandInterceptor
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
        if (!sql.StartsWith('-'))
        {
            return;
        }

        var excludePropertyKey = $"-- {QueryableExcludeExtensions.ExcludedPropertyAnnotation}";
        if (sql.StartsWith(excludePropertyKey, StringComparison.Ordinal))
        {
            var endIndex = sql.IndexOfAny(['\n', '\r']);
            var excluded = sql[excludePropertyKey.Length..endIndex];

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
