namespace Monica.AI.RAG.Models;

/// <summary>
/// Result of a non-destructive vector-store connectivity probe.
/// </summary>
public sealed record VectorStoreConnectionTestResult
{
    /// <summary>
    /// Whether the vector-store probe completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Probe collection name used for the connectivity check.
    /// </summary>
    public required string ProbeCollectionName { get; init; }

    /// <summary>
    /// Whether the probe collection already exists.
    /// </summary>
    public bool? ProbeCollectionExists { get; init; }

    /// <summary>
    /// Elapsed probe duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Human-readable success or failure details.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// UTC time when the test completed.
    /// </summary>
    public DateTimeOffset TestedAt { get; init; } = DateTimeOffset.UtcNow;
}
