namespace Monica.Profiling.ExecutionTiming.Abstractions.Internal;

/// <summary>
/// Coordinates execution-timing writes and exposes query access for the current aggregation mode.
/// </summary>
internal interface IExecutionTimingCoordinator : IExecutionTimingQuery
{
    /// <summary>
    /// Registers that a timing sample has started.
    /// </summary>
    void RegisterStart(
        Guid invocationId,
        string operationKey,
        string displayName,
        DateTimeOffset startedAt,
        string? description);

    /// <summary>
    /// Completes a timing sample and forwards it to the active aggregation strategy.
    /// </summary>
    void CompleteSample(
        Guid invocationId,
        string operationKey,
        string displayName,
        long durationMs,
        long? memoryBytes);
}
