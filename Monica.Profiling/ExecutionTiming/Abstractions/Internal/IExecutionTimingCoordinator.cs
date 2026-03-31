using Monica.Profiling.ExecutionTiming.Abstractions;

namespace Monica.Profiling.ExecutionTiming.Abstractions.Internal;

/// <summary>
/// Coordinates execution-timing writes and exposes query access for the current aggregation mode.
/// </summary>
internal interface IExecutionTimingCoordinator : IExecutionTimingQuery
{
    /// <summary>
    /// Registers that a timing sample has started.
    /// </summary>
    void RegisterStart(string name, DateTimeOffset startedAt, string? description);

    /// <summary>
    /// Completes a timing sample and forwards it to the active aggregation strategy.
    /// </summary>
    void CompleteSample(string name, long durationMs, string? description, long? memoryBytes);
}
