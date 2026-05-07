using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Monica.Repository.Persistence.Metrics;

/// <summary>
/// Bridges EF Core connection lifecycle interception to Repository persistence metrics.
/// </summary>
internal sealed class EfCoreConnectionMetricsInterceptor(EfCoreConnectionMetrics metrics) : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<DbConnection> ConnectionCreating(
        ConnectionCreatingEventData eventData,
        InterceptionResult<DbConnection> result)
    {
        metrics.RecordConnectionCreating();
        return base.ConnectionCreating(eventData, result);
    }

    /// <inheritdoc />
    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection result)
    {
        metrics.RecordConnectionCreated();
        return base.ConnectionCreated(eventData, result);
    }

    /// <inheritdoc />
    public override InterceptionResult ConnectionClosing(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        metrics.RecordConnectionClosing();
        return base.ConnectionClosing(connection, eventData, result);
    }

    /// <inheritdoc />
    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        metrics.RecordConnectionClosed();
        base.ConnectionClosed(connection, eventData);
    }

    /// <inheritdoc />
    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        metrics.RecordConnectionFailed();
        base.ConnectionFailed(connection, eventData);
    }

    /// <inheritdoc />
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        metrics.RecordConnectionOpening();
        return base.ConnectionOpening(connection, eventData, result);
    }

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        metrics.RecordConnectionOpened();
        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override InterceptionResult ConnectionDisposing(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        metrics.RecordConnectionDisposing();
        return base.ConnectionDisposing(connection, eventData, result);
    }

    /// <inheritdoc />
    public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData)
    {
        metrics.RecordConnectionDisposed();
        base.ConnectionDisposed(connection, eventData);
    }
}
