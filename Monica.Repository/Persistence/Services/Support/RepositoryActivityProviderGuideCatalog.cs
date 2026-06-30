using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Resolves provider-specific Repository activity diagnostics guides.
/// </summary>
public static class RepositoryActivityProviderGuideCatalog
{
    private static readonly string[] COMMON_COLUMNS =
    [
        "Runtime",
        "Datname",
        "Usename",
        "ApplicationName",
        "ClientAddr",
        "ClientPort",
        "Waiting",
        "State",
        "Query"
    ];

    /// <summary>
    /// Resolves the manual activity diagnostics guide for an EF Core provider name.
    /// </summary>
    /// <param name="providerName">EF Core provider name reported by the selected DbContext.</param>
    /// <returns>A supported guide when the provider is recognized; otherwise an unsupported guide.</returns>
    public static RepositoryActivityProviderGuide Resolve(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return CreateUnsupported(providerName);
        }

        if (providerName.Contains("OpenGauss", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("Gauss", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("Vastbase", StringComparison.OrdinalIgnoreCase))
        {
            return new RepositoryActivityProviderGuide
            {
                ProviderKind = RepositoryActivityProviderKind.OpenGauss,
                ProviderName = providerName,
                DisplayName = "openGauss",
                SourceObject = "pg_stat_activity",
                Sql = OpenGaussActivityQuery.GetSql(),
                SelectedColumns =
                [
                    .. COMMON_COLUMNS,
                    "Sessionid",
                    "ResourcePool",
                    "QueryId",
                    "ConnectionInfo",
                    "UniqueSqlId",
                    "TraceId"
                ]
            };
        }

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("Postgre", StringComparison.OrdinalIgnoreCase))
        {
            return new RepositoryActivityProviderGuide
            {
                ProviderKind = RepositoryActivityProviderKind.PostgreSql,
                ProviderName = providerName,
                DisplayName = "PostgreSQL",
                SourceObject = "pg_stat_activity",
                Sql = PostgreSqlActivityQuery.GetSql(),
                SelectedColumns =
                [
                    .. COMMON_COLUMNS,
                    "Pid",
                    "BackendStart",
                    "XactStart",
                    "QueryStart",
                    "StateChange"
                ]
            };
        }

        return CreateUnsupported(providerName);
    }

    private static RepositoryActivityProviderGuide CreateUnsupported(string? providerName)
    {
        return new RepositoryActivityProviderGuide
        {
            ProviderKind = RepositoryActivityProviderKind.Unsupported,
            ProviderName = providerName,
            DisplayName = string.IsNullOrWhiteSpace(providerName) ? string.Empty : providerName
        };
    }
}
