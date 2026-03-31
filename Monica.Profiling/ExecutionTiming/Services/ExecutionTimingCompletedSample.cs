namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Represents a completed execution-timing sample waiting to be aggregated.
/// </summary>
internal readonly record struct ExecutionTimingCompletedSample(
    string Name,
    long DurationMs,
    DateTimeOffset RecordedAt,
    long? MemoryBytes);
