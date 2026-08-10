namespace Monica.HealthCheck.Models;

/// <summary>
/// Represents one sanitized health-check result from the current host.
/// </summary>
public sealed class HealthCheckEntrySnapshot
{
    /// <summary>
    /// Gets the unique registration name supplied by the module or host.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the health state returned by the registration.
    /// </summary>
    public required HealthCheckState Status { get; init; }

    /// <summary>
    /// Gets the optional human-readable description returned by the check.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the time spent executing this check.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the stable classification tags attached to the registration.
    /// </summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    /// Gets bounded diagnostic values. Sensitive keys are retained but their values are redacted.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Data { get; init; }

    /// <summary>
    /// Gets bounded exception information when the health check failed with an exception.
    /// </summary>
    public HealthCheckErrorSnapshot? Error { get; init; }
}
