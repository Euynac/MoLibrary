namespace Monica.Profiling.ExecutionTiming.Models;

/// <summary>
/// Aggregated timing statistics for a stable operation identity.
/// </summary>
/// <param name="OperationKey">Stable machine identity of the operation.</param>
/// <param name="DisplayName">Human-readable operation name.</param>
/// <param name="ExecutionCount">Number of completed samples recorded for this operation.</param>
/// <param name="AverageDurationMs">Average duration, in milliseconds, across all recorded samples.</param>
/// <param name="FirstRecordedAt">Timestamp of the first completed sample.</param>
public sealed record ExecutionTimingStatistics(
    string OperationKey,
    string DisplayName,
    int ExecutionCount,
    double AverageDurationMs,
    DateTimeOffset FirstRecordedAt)
{
    /// <summary>
    /// Average allocated bytes across all recorded samples when memory tracking was enabled.
    /// </summary>
    public long? AverageMemoryBytes { get; init; }

    /// <summary>
    /// Allocated bytes captured for the most recent sample when memory tracking was enabled.
    /// </summary>
    public long? LastMemoryBytes { get; init; }

    /// <summary>
    /// Duration, in milliseconds, of the most recent sample.
    /// </summary>
    public long LastDurationMs { get; init; }

    /// <summary>
    /// Timestamp of the most recent completed sample.
    /// </summary>
    public DateTimeOffset? LastRecordedAt { get; init; }
}
