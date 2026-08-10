namespace Monica.HealthCheck;

/// <summary>
/// Defines stable tags used to classify Monica health-check registrations.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Identifies checks that prove the process is alive and able to answer requests.
    /// </summary>
    public const string Live = "live";

    /// <summary>
    /// Identifies checks that must pass before the host is ready to receive workload traffic.
    /// </summary>
    public const string Ready = "ready";

    /// <summary>
    /// Identifies checks contributed by Monica modules.
    /// </summary>
    public const string Monica = "monica";
}
