namespace Monica.Configuration.Metrics;

/// <summary>
/// OpenTelemetry meter and instrument names emitted by Monica.Configuration.
/// </summary>
public static class ConfigurationMetrics
{
    /// <summary>
    /// Gets the meter name for Monica.Configuration.
    /// </summary>
    public const string MeterName = "Monica.Configuration";

    /// <summary>
    /// Gets the mutation counter name.
    /// </summary>
    public const string MutationCount = "monica.configuration.mutation.count";

    /// <summary>
    /// Gets the reload latency histogram name.
    /// </summary>
    public const string ReloadLatency = "monica.configuration.reload.latency";

    /// <summary>
    /// Gets the mutation failure counter name.
    /// </summary>
    public const string MutationFailureCount = "monica.configuration.mutation.failure.count";

    /// <summary>
    /// Gets the reload notification failure counter name.
    /// </summary>
    public const string ReloadNotificationFailureCount = "monica.configuration.reload.notification.failure.count";
}
