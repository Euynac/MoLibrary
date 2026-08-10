namespace Monica.HealthCheck.Models;

/// <summary>
/// Represents a sanitized, point-in-time health report for the current host.
/// </summary>
public sealed class HealthCheckSnapshot
{
    /// <summary>
    /// Gets the registration scope used to execute this snapshot.
    /// </summary>
    public required HealthCheckScope Scope { get; init; }

    /// <summary>
    /// Gets the aggregate state reported by ASP.NET Core health checks.
    /// </summary>
    public required HealthCheckState Status { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which execution completed.
    /// </summary>
    public required DateTimeOffset CheckedAt { get; init; }

    /// <summary>
    /// Gets the total execution duration for all selected checks.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the selected check results ordered by registration name.
    /// </summary>
    public required IReadOnlyList<HealthCheckEntrySnapshot> Entries { get; init; }

    /// <summary>
    /// Gets the number of healthy entries.
    /// </summary>
    public int HealthyCount => Entries.Count(static entry => entry.Status == HealthCheckState.Healthy);

    /// <summary>
    /// Gets the number of degraded entries.
    /// </summary>
    public int DegradedCount => Entries.Count(static entry => entry.Status == HealthCheckState.Degraded);

    /// <summary>
    /// Gets the number of unhealthy entries.
    /// </summary>
    public int UnhealthyCount => Entries.Count(static entry => entry.Status == HealthCheckState.Unhealthy);
}
