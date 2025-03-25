
using System.Net;

namespace MoLibrary.Framework.Features.EfCoreExtensions.ActivityStatus;

//SELECT * FROM pg_stat_statements 需要启用PGSQL分析



//pg_stat_activity
public class PgSqlActivityStatus
{
    /// <summary>
    /// 已经执行的时间  current_timestamp - query_start
    /// </summary>
    public TimeSpan? Runtime { get; set; }
    ///// <summary>
    ///// The OID of the database to which the user session is connected in the background. 用户会话在后台连接到的数据库OID
    ///// </summary>
    //public int? Datid { get; set; }

    /// <summary>
    /// The name of the database to which the user session is connected in the background. 用户会话在后台连接到的数据库名称。
    /// </summary>
    public string? Datname { get; set; }

    ///// <summary>
    ///// The backend thread ID. 后台线程ID。
    ///// </summary>
    //public long? Pid { get; set; }

    ///// <summary>
    ///// The OID of the user logged into the backend.  登录该后台的用户OID。
    ///// </summary>
    //public long? Usesysid { get; set; }

    /// <summary>
    /// The username of the user logged into the backend. 登录该后台的用户名。
    /// </summary>
    public string? Usename { get; set; }

    /// <summary>
    /// The name of the application connected to the backend.  连接到该后台的应用名。
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// The IP address of the client connected to the backend. If null, it indicates a Unix socket connection or an internal process like autovacuum.
    /// 连接到该后台的客户端的IP地址。 如果此字段是null，它表明通过服务器机器上UNIX套接字连接客户端或者这是内部进程，如autovacuum。
    /// </summary>
    public IPAddress? ClientAddr { get; set; }

    /// <summary>
    /// The hostname of the client, obtained through a reverse DNS lookup of client_addr. Non-null only if log_hostname is enabled and IP connection is used.
    /// 客户端的主机名，这个字段是通过client_addr的反向DNS查找得到。这个字段只有在启动log_hostname且使用IP连接时才非空。
    /// </summary>
    public string? ClientHostname { get; set; }

    /// <summary>
    /// The TCP port number that the client uses to communicate with the backend. -1 if using Unix sockets.
    /// 客户端用于与后台通讯的TCP端口号，如果使用Unix套接字，则为-1。
    /// </summary>
    public int? ClientPort { get; set; }

    /// <summary>
    /// The time the process started, which is when the client connected to the server. 该过程开始的时间，即当客户端连接服务器时。
    /// </summary>
    public DateTime? BackendStart { get; set; }

    /// <summary>
    /// The time the current transaction started. Null if no transaction is active. Equal to query_start if the current query is the first transaction.
    /// 启动当前事务的时间，如果没有事务是活跃的，则为null。如果当前查询是首个事务，则这列等同于query_start列。
    /// </summary>
    public DateTime? XactStart { get; set; }

    /// <summary>
    /// The time the current active query started. If the state is not active, this is the start time of the last query.
    /// 开始当前活跃查询的时间， 如果state的值不是active，则这个值是上一个查询的开始时间。
    /// </summary>
    public DateTime? QueryStart { get; set; }

    /// <summary>
    /// The time the state was last changed.
    /// 上次状态改变的时间。
    /// </summary>
    public DateTime? StateChange { get; set; }

    /// <summary>
    /// True if the backend is currently waiting for a lock. 如果后台当前正等待锁则为true。
    /// </summary>
    public bool? Waiting { get; set; }

    /// <summary>
    /// The current overall state of the backend. Possible values are: 该后台当前总体状态。可能值是：
    /// active: The backend is executing a query.
    /// idle: The backend is waiting for a new client command.
    /// idle in transaction: The backend is in a transaction, but not executing a statement.
    /// idle in transaction (aborted): The backend is in a transaction, but a statement execution failed.
    /// fast path function call: The backend is executing a fast-path function.
    /// disabled: track_activities is disabled.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// The most recent query of the backend. If the state is active, this field shows the currently executing query. Otherwise, it shows the last query.
    /// 该后台的最新查询。如果state状态是active（活跃的），此字段显示当前正在执行的查询。所有其他情况表示上一个查询。
    /// </summary>
    public string? Query { get; set; }

    public static string GetQuerySql(bool gaussDb = false)
    {
        if (gaussDb)
        {
            
            return """
                   SELECT
                       current_timestamp - query_start AS Runtime,
                       datid AS Datid,
                       datname AS Datname,
                       pid AS Pid,
                       usesysid AS Usesysid,
                       usename AS Usename,
                       application_name AS ApplicationName,
                       client_addr AS ClientAddr,
                       client_hostname AS ClientHostname,
                       client_port AS ClientPort,
                       backend_start AS BackendStart,
                       xact_start AS XactStart,
                       query_start AS QueryStart,
                       state_change AS StateChange,
                       waiting AS Waiting,
                       state AS State,
                       query AS Query
                   FROM
                       pg_stat_activity;
                   """;
        }

        return """
               SELECT
                   current_timestamp - query_start AS Runtime,
                   datid AS Datid,
                   datname AS Datname,
                   pid AS Pid,
                   usesysid AS Usesysid,
                   usename AS Usename,
                   application_name AS ApplicationName,
                   client_addr AS ClientAddr,
                   client_hostname AS ClientHostname,
                   client_port AS ClientPort,
                   backend_start AS BackendStart,
                   xact_start AS XactStart,
                   query_start AS QueryStart,
                   state_change AS StateChange,
                   CASE
                       WHEN wait_event_type = 'Lock' THEN true
                       ELSE false
                   END AS Waiting,
                   state AS State,
                   query AS Query
               FROM
                   pg_stat_activity;
               """;
    }
}