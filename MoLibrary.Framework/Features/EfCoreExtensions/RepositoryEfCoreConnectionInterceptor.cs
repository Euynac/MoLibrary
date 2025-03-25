using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MoLibrary.Framework.Features.EfCoreExtensions;

public class RepositoryEfCoreConnectionInterceptor : DbConnectionInterceptor
{

    public override InterceptionResult<DbConnection> ConnectionCreating(ConnectionCreatingEventData eventData, InterceptionResult<DbConnection> result)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionCreatingCount);
        return base.ConnectionCreating(eventData, result);
    }

    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection result)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionCreatedCount); ;
        return base.ConnectionCreated(eventData, result);
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionClosedCount); ;
        base.ConnectionClosed(connection, eventData);
    }

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionClosingCount); ;
        return base.ConnectionClosing(connection, eventData, result);
    }

    public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionDisposedCount); ;
        base.ConnectionDisposed(connection, eventData);
    }

    public override InterceptionResult ConnectionDisposing(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionDisposingCount); ;
        return base.ConnectionDisposing(connection, eventData, result);
    }

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionFailedCount); ;
        base.ConnectionFailed(connection, eventData);
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionOpeningCount); ;
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionStatus.ConnectionOpenedCount); ;
        base.ConnectionOpened(connection, eventData);
    }
}


public class EfCoreConnectionStatus
{
    public int CurrentCreatingCount => ConnectionCreatingCount - ConnectionCreatedCount;
    public int CurrentDisposingCount => ConnectionDisposingCount - ConnectionDisposedCount;
    public int CurrentNotClosedCount => ConnectionCreatedCount - ConnectionDisposedCount;
    public int TotalCreatingCount => ConnectionCreatingCount;
    public int TotalCreatedCount => ConnectionCreatedCount;

    public int TotalClosingCount => ConnectionClosingCount;
    public int TotalClosedCount => ConnectionClosedCount;
    public int TotalFailedCount => ConnectionFailedCount;
    public int TotalOpeningCount => ConnectionOpeningCount;
    public int TotalOpenedCount => ConnectionOpenedCount;
    public int TotalDisposingCount => ConnectionDisposingCount;
    public int TotalDisposedCount => ConnectionDisposedCount;

    public static int ConnectionCreatingCount;
    public static int ConnectionCreatedCount;

    public static int ConnectionClosingCount;
    public static int ConnectionClosedCount;
    public static int ConnectionFailedCount;
    public static int ConnectionOpeningCount;
    public static int ConnectionOpenedCount;
    public static int ConnectionDisposingCount;
    public static int ConnectionDisposedCount;

    //private static readonly ConcurrentQueue<Action> _updatedActions = new();
    //static EfCoreConnectionStatus()
    //{
    //    Task.Factory.StartNew(async () =>
    //    {
    //        while (true)
    //        {
    //            if (_updatedActions.TryDequeue(out var action))
    //            {
    //                action.Invoke();
    //            }

    //            await Task.Delay(1);
    //        }
    //    }, TaskCreationOptions.LongRunning);
    //}
    //internal void UpdateValue(Action action)
    //{
    //    _updatedActions.Enqueue(action);
    //}
}