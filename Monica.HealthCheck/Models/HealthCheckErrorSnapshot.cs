namespace Monica.HealthCheck.Models;

/// <summary>
/// Contains bounded exception information safe for an authenticated operational surface.
/// </summary>
public sealed class HealthCheckErrorSnapshot
{
    /// <summary>
    /// Gets the exception type name without assembly, stack-trace, or object details.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the bounded exception message reported by the check.
    /// </summary>
    public required string Message { get; init; }
}
