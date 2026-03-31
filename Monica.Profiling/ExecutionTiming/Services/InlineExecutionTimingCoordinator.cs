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

    public ExecutionTimingStatistics? GetStatistics(string name)
    {
        return collector.GetStatistics(name);
    }

    public IReadOnlyDictionary<string, RunningExecutionTimingInfo> GetRunningOperations()
    {
        return collector.GetRunningOperations();
    }

    public void Reset(string name)
    {
        collector.Reset(name);
    }

    public void RegisterStart(string name, DateTimeOffset startedAt, string? description)
    {
        collector.RegisterStart(name, startedAt, description);
    }

    public void CompleteSample(string name, long durationMs, string? description, long? memoryBytes)
    {
        collector.CompleteRunning(name);
        collector.ApplyRecord(new ExecutionTimingCompletedSample(
            name,
            durationMs,
            DateTimeOffset.UtcNow,
            memoryBytes));
    }
}
