using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Models;
using Monica.Tool.Extensions;

namespace Monica.Framework.ChainTracing.Providers.EntityFrameworkCore;

/// <summary>
/// Records EF Core command execution inside the current chain-tracing context.
/// </summary>
/// <param name="chainTracing">The chain tracing service.</param>
/// <param name="logger">The logger.</param>
public class ChainTracingDbCommandInterceptor(
    IChainTracing chainTracing,
    ILogger<ChainTracingDbCommandInterceptor> logger) : DbCommandInterceptor
{
    private readonly ConcurrentDictionary<object, string> _commandTraceMap = new();

    private string StartCommandTrace(DbCommand command)
    {
        try
        {
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

            var traceId = chainTracing.BeginTrace(
                command.CommandText.LimitMaxLength(1000, "..."),
                null,
                extraInfo,
                EChainTracingType.Database);

            _commandTraceMap[command] = traceId;
            return traceId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记录数据库命令开始时发生异常");
            return string.Empty;
        }
    }

    private void FinishCommandTrace(string traceId, CommandEventData eventData, object? result = null, bool isCanceled = false)
    {
        if (string.IsNullOrEmpty(traceId))
        {
            return;
        }

        try
        {
            string resultDescription;
            var success = true;
            Exception? exception = null;

            if (eventData is CommandEndEventData endEvent)
            {
                resultDescription = "Success";

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
                else if (result is int affectedRows)
                {
                    resultDescription += $"[AffectedRows:{affectedRows}]";
                }

                resultDescription += $"[{endEvent.Duration.TotalMilliseconds:0.##}ms]";
            }
            else
            {
                resultDescription = "Unknown";
            }

            chainTracing.EndTrace(traceId, resultDescription, success, exception);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记录数据库命令结束时发生异常");
        }
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        StartCommandTrace(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            FinishCommandTrace(traceId, eventData, result);
        }

        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        StartCommandTrace(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            FinishCommandTrace(traceId, eventData, result);
        }

        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        StartCommandTrace(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            FinishCommandTrace(traceId, eventData, result);
        }

        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        StartCommandTrace(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            FinishCommandTrace(traceId, eventData, result);
        }

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            FinishCommandTrace(traceId, eventData);
        }

        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_commandTraceMap.TryRemove(command, out var traceId))
        {
            FinishCommandTrace(traceId, eventData, isCanceled: true);
        }

        return base.CommandCanceledAsync(command, eventData, cancellationToken);
    }
}
