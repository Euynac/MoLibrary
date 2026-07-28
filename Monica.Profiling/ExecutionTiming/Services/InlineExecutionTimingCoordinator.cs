using Monica.Profiling.ExecutionTiming.Abstractions.Internal;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Aggregates execution-timing samples immediately on the caller thread.
/// </summary>
internal sealed class InlineExecutionTimingCoordinator(ExecutionTimingCollector collector) : IExecutionTimingCoordinator
{
    public IReadOnlyDictionary<string, ExecutionTimingStatistics> GetStatistics()
    {
        return collector.GetStatistics();
    }

    public ExecutionTimingStatistics? GetStatistics(string operationKey)
    {
        return collector.GetStatistics(operationKey);
    }

    public IReadOnlyDictionary<Guid, RunningExecutionTimingInfo> GetRunningOperations()
    {
        return collector.GetRunningOperations();
    }

    public void Reset(string operationKey)
    {
        collector.Reset(operationKey);
    }

    public void RegisterStart(
        Guid invocationId,
        string operationKey,
        string displayName,
        DateTimeOffset startedAt,
        string? description)
    {
        collector.RegisterStart(invocationId, operationKey, displayName, startedAt, description);
    }

    public void CompleteSample(
        Guid invocationId,
        string operationKey,
        string displayName,
        long durationMs,
        long? memoryBytes)
    {
        collector.CompleteRunning(invocationId);
        collector.ApplyRecord(new ExecutionTimingCompletedSample(
            operationKey,
            displayName,
            durationMs,
            DateTimeOffset.UtcNow,
            memoryBytes));
    }
}
