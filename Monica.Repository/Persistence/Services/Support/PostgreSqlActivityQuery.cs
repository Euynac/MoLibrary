namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Provides the PostgreSQL <c>pg_stat_activity</c> query used by diagnostics endpoints.
/// </summary>
public static class PostgreSqlActivityQuery
{
    public static string GetSql()
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
