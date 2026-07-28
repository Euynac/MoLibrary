using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Abstractions;

/// <summary>
/// Provides read access to collected execution-timing statistics and running measurements.
/// </summary>
public interface IExecutionTimingQuery
{
    /// <summary>
    /// Returns a snapshot of all completed timing statistics keyed by stable operation key.
    /// </summary>
    IReadOnlyDictionary<string, ExecutionTimingStatistics> GetStatistics();

    /// <summary>
    /// Returns the completed timing statistics for a single stable operation key.
    /// </summary>
    /// <param name="operationKey">Stable machine identity of the measured operation.</param>
    ExecutionTimingStatistics? GetStatistics(string operationKey);

    /// <summary>
    /// Returns a snapshot of all currently running timing scopes keyed by invocation ID.
    /// </summary>
    IReadOnlyDictionary<Guid, RunningExecutionTimingInfo> GetRunningOperations();

    /// <summary>
    /// Removes the completed statistics for a single stable operation key.
    /// </summary>
    /// <param name="operationKey">Stable machine identity of the measured operation.</param>
    void Reset(string operationKey);
}
