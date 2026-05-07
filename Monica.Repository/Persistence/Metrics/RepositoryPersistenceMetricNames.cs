namespace Monica.Repository.Persistence.Metrics;

/// <summary>
/// Defines meter and instrument names emitted by the Repository persistence feature.
/// </summary>
public static class RepositoryPersistenceMetricNames
{
    /// <summary>
    /// Meter name used for Repository persistence metrics.
    /// </summary>
    public const string MeterName = "Monica.Repository.Persistence";

    /// <summary>
    /// Counter instrument that records EF Core connection lifecycle events observed by Monica Repository.
    /// </summary>
    public const string EfCoreConnectionEvents = "monica.repository.persistence.ef_core.connection.events";

    /// <summary>
    /// Observable gauge instrument that reports current EF Core connection lifecycle state observed by Monica Repository.
    /// </summary>
    public const string EfCoreConnectionCurrent = "monica.repository.persistence.ef_core.connection.current";
}
