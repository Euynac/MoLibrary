namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Provides the openGauss <c>pg_stat_activity</c> query used by diagnostics endpoints.
/// </summary>
public static class OpenGaussActivityQuery
{
    public static string GetSql()
    {
        return """
               SELECT
                   current_timestamp - query_start AS Runtime,
                   datid AS Datid,
                   datname AS Datname,
                   pid AS Pid,
                   sessionid AS Sessionid,
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
                   resource_pool AS ResourcePool,
                   query_id AS QueryId,
                   query AS Query,
                   connection_info AS ConnectionInfo,
                   unique_sql_id AS UniqueSqlId,
                   trace_id AS TraceId
               FROM
                   pg_stat_activity;
               """;
    }
}
