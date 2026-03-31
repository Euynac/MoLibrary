using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Abstractions;

/// <summary>
/// Provides read access to collected execution-timing statistics and running measurements.
/// </summary>
public interface IExecutionTimingQuery
{
    /// <summary>
    /// Returns a snapshot of all completed timing statistics keyed by operation name.
    /// </summary>
    IReadOnlyDictionary<string, ExecutionTimingStatistics> GetStatistics();

    /// <summary>
    /// Returns the completed timing statistics for a single operation name.
    /// </summary>
    /// <param name="name">Logical name of the measured operation.</param>
    ExecutionTimingStatistics? GetStatistics(string name);

    /// <summary>
    /// Returns a snapshot of all currently running timing scopes keyed by operation name.
    /// </summary>
    IReadOnlyDictionary<string, RunningExecutionTimingInfo> GetRunningOperations();

    /// <summary>
    /// Removes the completed statistics for a single operation name.
    /// </summary>
    /// <param name="name">Logical name of the measured operation.</param>
    void Reset(string name);
}
