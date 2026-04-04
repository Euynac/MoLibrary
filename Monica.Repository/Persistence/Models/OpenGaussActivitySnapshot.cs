namespace Monica.Repository.Persistence.Models;

/// <summary>
/// Represents a row returned from openGauss <c>pg_stat_activity</c>.
/// </summary>
public class OpenGaussActivitySnapshot
{
    /// <summary>
    /// Execution time current_timestamp - query_start
    /// </summary>
    public TimeSpan? Runtime { get; set; }
    /// <summary>
    /// The OID of the database to which the user session is connected in the background.
    /// </summary>
    public long? Datid { get; set; }

    /// <summary>
    /// The name of the database to which the user session is connected in the background.
    /// </summary>
    public string? Datname { get; set; }

    /// <summary>
    /// The backend thread ID. The background thread ID.
    /// </summary>
    public long? Pid { get; set; }

    /// <summary>
    /// The session ID.
    /// </summary>
    public long? Sessionid { get; set; }

    /// <summary>
    /// The OID of the user logged into the backend. The OID of the user logged into the backend.
    /// </summary>
    public long? Usesysid { get; set; }

    /// <summary>
    /// The username of the user logged into the backend. The username of the user logged into the backend.
    /// </summary>
    public string? Usename { get; set; }

    /// <summary>
    /// The name of the application connected to the backend. The name of the application connected to the backend.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// The IP address of the client connected to the backend. If null, it indicates a Unix socket connection or an internal process like autovacuum.
    /// The IP address of the client connecting to this backend. If this field is null, it indicates that the client is connected via a UNIX socket on the server machine or this is an internal process such as autovacuum.
    /// </summary>
    public string? ClientAddr { get; set; }

    /// <summary>
    /// The hostname of the client, obtained through a reverse DNS lookup of client_addr. Non-null only if log_hostname is enabled and IP connection is used.
    /// The host name of the client. This field is obtained through a reverse DNS lookup of client_addr. This field is only non-empty when log_hostname is enabled and an IP connection is used.
    /// </summary>
    public string? ClientHostname { get; set; }

    /// <summary>
    /// The TCP port number that the client uses to communicate with the backend. -1 if using Unix sockets.
    /// The TCP port number used by the client to communicate with the backend, or -1 if using a Unix socket.
    /// </summary>
    public int ClientPort { get; set; }

    /// <summary>
    /// The time the process started, which is when the client connected to the server.
    /// </summary>
    public DateTime? BackendStart { get; set; }

    /// <summary>
    /// The time the current transaction started. Null if no transaction is active. Equal to query_start if the current query is the first transaction.
    /// The time when the current transaction was started, or null if no transactions are active. If the current query is the first transaction, this column is equivalent to the query_start column.
    /// </summary>
    public DateTime? XactStart { get; set; }

    /// <summary>
    /// The time the current active query started. If the state is not active, this is the start time of the last query.
    /// The time to start the current active query. If the value of state is not active, this value is the start time of the previous query.
    /// </summary>
    public DateTime? QueryStart { get; set; }

    /// <summary>
    /// The time the state was last changed.
    /// The time of the last status change.
    /// </summary>
    public DateTime? StateChange { get; set; }

    /// <summary>
    /// True if the backend is currently waiting for a lock. True if the backend is currently waiting for a lock.
    /// </summary>
    public bool? Waiting { get; set; }

    /// <summary>
    /// The current overall state of the backend. Possible values ​​are: The current overall state of the backend. Possible values ​​are:
    /// active: The backend is executing a query.
    /// idle: The backend is waiting for a new client command.
    /// idle in transaction: The backend is in a transaction, but not executing a statement.
    /// idle in transaction (aborted): The backend is in a transaction, but a statement execution failed.
    /// fast path function call: The backend is executing a fast-path function.
    /// disabled: track_activities is disabled.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// The name of the resource pool used by the user. The resource pool used by the user.
    /// </summary>
    public string? ResourcePool { get; set; }

    /// <summary>
    /// The ID of the query statement. The ID of the query statement.
    /// </summary>
    public long? QueryId { get; set; }

    /// <summary>
    /// The most recent query of the backend. If the state is active, this field shows the currently executing query. Otherwise, it shows the last query.
    /// The latest query of this background. If the state is active, this field displays the currently executing query. All other cases represent the previous query.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// A JSON formatted string? that records the current connection database driver type, driver version, driver deployment path, and process owner user, etc.
    ///  json format string, records the driver type, driver version number, current driver deployment path, process owner user and other information currently connected to the database (see connection_info).
    /// </summary>
    public string? ConnectionInfo { get; set; }

    /// <summary>
    /// The unique SQL ID of the statement. The unique SQL ID of the statement.
    /// </summary>
    public long? UniqueSqlId { get; set; }

    /// <summary>
    /// The trace ID passed by the driver, associated with an application request. The trace ID passed by the driver is associated with an application request.
    /// </summary>
    public string? TraceId { get; set; }
}
