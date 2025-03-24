using System.Data.Common;
using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.Features.Decorators;
using MoLibrary.Tool.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocksPlatform.Features.EfCoreExtensions;

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

#region 旧版备份

//public class RepositoryChainRecorderInterceptor(OurRequestContext context) : AbpInterceptor, ITransientDependency
//{

//    private static bool ShouldRecordChain(IAbpMethodInvocation invocation, out string? entityType, out string? sql, out string? method)
//    {
//        method = invocation.Method.Name;
//        entityType = null;
//        sql = null;
//        // the method first argument is IQueryable
//        if(invocation.Arguments.FirstOrDefault() is IQueryable<dynamic> queryable)
//        {
//            entityType = queryable.ElementType.FullName;
//            sql = queryable.ToQueryString();
//            return true;
//        }

//        return false;
//    }

//    public override async Task InterceptAsync(IAbpMethodInvocation invocation)
//    {
//        var shouldRecordChain = ShouldRecordChain(invocation, out var entityType, out var sql, out var method);
//        if (shouldRecordChain)
//        {
//            context.ChainBridge ??= new InvokeChainInfo();
//            context.ChainBridge = context.ChainBridge.Invoking($"Db entity {entityType} do {method?.RemovePreFix("Record")}", sql ?? "");
//        }


//        await invocation.ProceedAsync();

//        if (shouldRecordChain)
//        {
//            context.ChainBridge =
//                context.ChainBridge!.Invoked();
//        }
//    }
//}

//public interface IRepositoryChainRecorder
//{
//    public Task<List<TEntity>> RecordToListAsync<TEntity>(IQueryable<TEntity> queryable, Func<IQueryable<TEntity>, Task<List<TEntity>>> func);
//    public Task<int> RecordCountAsync<TEntity>(IQueryable<TEntity> queryable, Func<IQueryable<TEntity>, Task<int>> func);
//    public Task<List<dynamic>> RecordDynamicToListAsync<TEntity>(IQueryable<TEntity> queryable, Func<IQueryable<TEntity>, Task<List<dynamic>>> func);
//}

//public class RepositoryChainRecorder : IRepositoryChainRecorder, ITransientDependency
//{
//    public Task<List<TEntity>> RecordToListAsync<TEntity>(IQueryable<TEntity> queryable, Func<IQueryable<TEntity>, Task<List<TEntity>>> func)
//    {
//        return func.Invoke(queryable);
//    }

//    public Task<int> RecordCountAsync<TEntity>(IQueryable<TEntity> queryable, Func<IQueryable<TEntity>, Task<int>> func)
//    {
//        return func.Invoke(queryable);
//    }

//    public Task<List<dynamic>> RecordDynamicToListAsync<TEntity>(IQueryable<TEntity> queryable, Func<IQueryable<TEntity>, Task<List<dynamic>>> func)
//    {
//        return func.Invoke(queryable);
//    }
//}

#endregion
