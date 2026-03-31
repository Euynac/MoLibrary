namespace Monica.Profiling.ExecutionTiming.Models;

/// <summary>
/// Controls how completed execution-timing samples are aggregated into statistics.
/// </summary>
public enum ExecutionTimingAggregationMode
{
    /// <summary>
    /// Updates statistics synchronously on the caller thread when a sample stops.
    /// </summary>
    Inline = 0,

    /// <summary>
    /// Queues completed samples and lets a background service aggregate them asynchronously.
    /// </summary>
    BackgroundBatch = 1
}
