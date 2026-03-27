using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoChainTracing;
using Monica.Core.Features.MoChainTracing.Models;
using Monica.Tool.Extensions;

namespace Monica.Framework.Features.FrameworkChainTracing;

/// <summary>
/// EF Core database operation link tracking interceptor based on the new ChainTracking system
/// Used to automatically record the execution of database commands into the call chain
/// </summary>
/// <param name="chainTracing">Call chain tracking service</param>
/// <param name="logger">Logger</param>
public class ChainTrackingProviderRepositoryEfCoreInterceptor(
    IMoChainTracing chainTracing, 
    ILogger<ChainTrackingProviderRepositoryEfCoreInterceptor> logger) : DbCommandInterceptor
{
    /// <summary>
    /// Store the mapping relationship between command and call chain TraceId
    /// </summary>
    private readonly ConcurrentDictionary<object, string> _commandTraceMap = new();
    /// <summary>
    /// Record database command starts execution
    /// </summary>
    /// <param name="command">Database commands</param>
    /// <param name="eventData">event data</param>
    /// <returns>Call chain node ID</returns>
    private string RecordDatabaseCommandStart(DbCommand command, CommandEventData eventData)
    {
        try
        {
            // Parsing SQL command types
            var commandText = command.CommandText;
            //var operationType = GetCommandType(commandText);
            //var tableName = ExtractTableName(commandText, operationType);
            
            //var operationName = string.IsNullOrEmpty(tableName) 
            //    ? operationType 
            //    : $"{operationType}({tableName})";

            // Prepare additional information
            var extraInfo = new
            {
                command.CommandTimeout,
                ParameterCount = command.Parameters.Count,
                Parameters = command.Parameters.Cast<DbParameter>()
                    .Take(10)
                    .Select(p => new
                    {
                        Name = p.ParameterName,
                        DbType = p.DbType.ToString(),
                        Value = p.Value?.ToString()?.LimitMaxLength(100, "...")
                    })
                    .ToArray()
            };

            // Start call chain tracing
            var traceId = chainTracing.BeginTrace(commandText.LimitMaxLength(1000, "..."), null, extraInfo, EChainTracingType.Database);
            
            // Store the mapping relationship between commands and TraceId
            _commandTraceMap[command] = traceId;
            
            return traceId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记录数据库命令开始时发生异常");
            return string.Empty;
        }
    }

    /// <summary>
    /// Record database command execution results
    /// </summary>
    /// <param name="traceId">Call chain node ID</param>
    /// <param name="eventData">event data</param>
    /// <param name="result">Execution result</param>
    /// <param name="isCanceled">Whether it was canceled</param>
    private void RecordDatabaseCommandEnd(string traceId, CommandEventData eventData, object? result = null, bool isCanceled = false)
    {
        if (string.IsNullOrEmpty(traceId))
            return;

        try
        {
            string resultDescription;
            var success = true;
            Exception? exception = null;

            if (eventData is CommandEndEventData endEvent)
            {
                var duration = endEvent.Duration;
                
                if (eventData is CommandErrorEventData errorEvent)
                {
                    success = false;
                    exception = errorEvent.Exception;
                    resultDescription = "Error";
                }
                else if (isCanceled)
                {
                    success = false;
                    resultDescription = "Canceled";
                }
                else
                {
                    resultDescription = "Success";
                    if(result is int affectedRows)
                    {
                        resultDescription += $"[AffectedRows:{affectedRows}]";
                    }
                }
                resultDescription += $"[{duration.TotalMilliseconds:0.##}ms]";
            }
            else
            {
                resultDescription = "未知状态";
            }

            chainTracing.EndTrace(traceId, resultDescription, success, exception);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记录数据库命令结束时发生异常");
        }
    }

    /// <summary>
    /// Get SQL command type
    /// </summary>
    /// <param name="commandText">SQL command text</param>
    /// <returns>Command type</returns>
    private static string GetCommandType(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return "Unknown";

        var upperCommand = commandText.ToUpperInvariant();
        
        if (upperCommand.StartsWith("SELECT"))
            return "SELECT";
        if (upperCommand.StartsWith("INSERT"))
            return "INSERT";
        if (upperCommand.StartsWith("UPDATE"))
            return "UPDATE";
        if (upperCommand.StartsWith("DELETE"))
            return "DELETE";
        if (upperCommand.StartsWith("CREATE"))
            return "CREATE";
        if (upperCommand.StartsWith("ALTER"))
            return "ALTER";
        if (upperCommand.StartsWith("DROP"))
            return "DROP";
        if (upperCommand.StartsWith("EXEC") || upperCommand.StartsWith("EXECUTE"))
            return "EXECUTE";

        return "Other";
    }

    /// <summary>
    /// Extract table name
    /// </summary>
    /// <param name="commandText">SQL command text</param>
    /// <param name="commandType">Command type</param>
    /// <returns>table name</returns>
    private static string? ExtractTableName(string commandText, string commandType)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return null;

        try
        {
            var upperCommand = commandText.ToUpperInvariant();
            
            return commandType switch
            {
                "SELECT" => ExtractTableFromSelect(upperCommand),
                "INSERT" => ExtractTableFromInsert(upperCommand),
                "UPDATE" => ExtractTableFromUpdate(upperCommand),
                "DELETE" => ExtractTableFromDelete(upperCommand),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractTableFromSelect(string upperCommand)
    {
        var fromIndex = upperCommand.IndexOf(" FROM ", StringComparison.Ordinal);
        if (fromIndex == -1) return null;

        var afterFrom = upperCommand[(fromIndex + 6)..].Trim();
        var spaceIndex = afterFrom.IndexOf(' ');
        return spaceIndex == -1 ? afterFrom : afterFrom[..spaceIndex];
    }

    private static string? ExtractTableFromInsert(string upperCommand)
    {
        var intoIndex = upperCommand.IndexOf(" INTO ", StringComparison.Ordinal);
        if (intoIndex == -1) return null;

        var afterInto = upperCommand[(intoIndex + 6)..].Trim();
        var spaceIndex = afterInto.IndexOf(' ');
        var parenIndex = afterInto.IndexOf('(');
        
        var endIndex = spaceIndex == -1 ? parenIndex : (parenIndex == -1 ? spaceIndex : Math.Min(spaceIndex, parenIndex));
        return endIndex == -1 ? afterInto : afterInto[..endIndex];
    }

    private static string? ExtractTableFromUpdate(string upperCommand)
    {
        var updateIndex = upperCommand.IndexOf("UPDATE ", StringComparison.Ordinal);
        if (updateIndex == -1) return null;

        var afterUpdate = upperCommand[(updateIndex + 7)..].Trim();
        var setIndex = afterUpdate.IndexOf(" SET ", StringComparison.Ordinal);
        return setIndex == -1 ? afterUpdate : afterUpdate[..setIndex].Trim();
    }

    private static string? ExtractTableFromDelete(string upperCommand)
    {
        var fromIndex = upperCommand.IndexOf(" FROM ", StringComparison.Ordinal);
        if (fromIndex == -1) return null;

        var afterFrom = upperCommand[(fromIndex + 6)..].Trim();
        var spaceIndex = afterFrom.IndexOf(' ');
        return spaceIndex == -1 ? afterFrom : afterFrom[..spaceIndex];
    }

    #region 数据库命令拦截方法

    // Write operation interception
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        RecordDatabaseCommandStart(command, eventData);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            RecordDatabaseCommandEnd(traceId, eventData, result);
        }
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        RecordDatabaseCommandStart(command, eventData);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            RecordDatabaseCommandEnd(traceId, eventData, result);
        }
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    // Read operation interception
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        RecordDatabaseCommandStart(command, eventData);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            RecordDatabaseCommandEnd(traceId, eventData, result);
        }
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        RecordDatabaseCommandStart(command, eventData);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            RecordDatabaseCommandEnd(traceId, eventData, result);
        }
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    // Exception handling
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            RecordDatabaseCommandEnd(traceId, eventData);
        }
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    // Cancel processing
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            RecordDatabaseCommandEnd(traceId, eventData, isCanceled: true);
        }
        return base.CommandCanceledAsync(command, eventData, cancellationToken);
    }

    #endregion
} 