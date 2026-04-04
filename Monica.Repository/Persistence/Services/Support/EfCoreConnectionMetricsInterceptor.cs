using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Tracks EF Core connection lifecycle events in process-wide counters for diagnostics endpoints.
/// </summary>
public class EfCoreConnectionMetricsInterceptor : DbConnectionInterceptor
{

    public override InterceptionResult<DbConnection> ConnectionCreating(ConnectionCreatingEventData eventData, InterceptionResult<DbConnection> result)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionCreatingCount);
        return base.ConnectionCreating(eventData, result);
    }

    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection result)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionCreatedCount); ;
        return base.ConnectionCreated(eventData, result);
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionClosedCount); ;
        base.ConnectionClosed(connection, eventData);
    }

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionClosingCount); ;
        return base.ConnectionClosing(connection, eventData, result);
    }

    public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionDisposedCount); ;
        base.ConnectionDisposed(connection, eventData);
    }

    public override InterceptionResult ConnectionDisposing(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionDisposingCount); ;
        return base.ConnectionDisposing(connection, eventData, result);
    }

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionFailedCount); ;
        base.ConnectionFailed(connection, eventData);
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionOpeningCount); ;
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Interlocked.Increment(ref EfCoreConnectionCounters.ConnectionOpenedCount); ;
        base.ConnectionOpened(connection, eventData);
    }
}
