using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features.FrameworkChainTracing;

public class RepositoryChainEfCoreRecorderInterceptor(IHttpContextAccessor accessor, ILogger<RepositoryChainEfCoreRecorderInterceptor> logger) : DbCommandInterceptor
{
    private void Record(DbCommand command, CommandEventData eventData, bool isCanceled = false)
    {
        var context = accessor.HttpContext?.GetOrNew<MoRequestContext>();
        if (context == null) return;

        if (eventData is CommandEndEventData endEvent)
        {
            var res = $"db duration:{endEvent.Duration.TotalMilliseconds:0.##}ms";

            if (eventData is CommandErrorEventData errorEvent)
            {
                res = $"[Error]{res};{errorEvent.Exception}";
            }
            else if (isCanceled)
            {
                res = $"[Canceled]{res}";
            }
            else
            {
                res = $"[Success]{res}";
            }

            context.Invoked(res);
        }
        else
        {
            var info = new
            {
                command.CommandTimeout,
                command.Parameters.Count,
                Params = command.Parameters.Cast<DbParameter>().Take(10).Select(p => new
                        { p.ParameterName, DbType = p.DbType.ToString(), p.SourceColumn, Value = p.Value?.ToString() })
                    .ToList()
            };
            if (info.Count == 0)
            {
                info = null;
            }
            context.Invoking($"Db operation", command.CommandText.LimitMaxLength(3333, "..."), info);
        }
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData,
        CancellationToken cancellationToken = new())
    {
        Record(command, eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData,
        CancellationToken cancellationToken = new())
    {
        Record(command, eventData, true);
        return base.CommandCanceledAsync(command, eventData, cancellationToken);
    }


    #region 写操作日志记录
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command, eventData);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = new())
    {
        Record(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        Record(command, eventData);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    #endregion



    #region 读操作日志记录

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command, eventData);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = new())
    {
        Record(command, eventData);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }


    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = new())
    {
        Record(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    #endregion


}