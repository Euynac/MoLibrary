namespace Monica.Profiling.ExecutionTiming.Abstractions;

/// <summary>
/// Represents a mutable timing recorder for a named operation.
/// </summary>
public interface IExecutionTimingRecorder : IDisposable
{
    /// <summary>
    /// Enables information-level logging when a sample is completed.
    /// </summary>
    bool EnableLogging { get; set; }

    /// <summary>
    /// Enables per-thread allocation tracking for the current timing sample.
    /// This is best-effort only and can be inaccurate across async continuations.
    /// </summary>
    [Obsolete("Memory tracking relies on thread-local allocation counters and can be inaccurate across async continuations.")]
    bool EnableMemoryTracking { get; set; }

    /// <summary>
    /// Optional display text used by running-state diagnostics and logs.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// Starts the current timing sample.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the current timing sample and records it in the aggregate statistics store.
    /// </summary>
    void Stop();
}
