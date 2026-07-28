using System.Collections.Concurrent;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Services;

internal sealed class ExecutionTimingCollector
{
    private readonly ConcurrentDictionary<string, ExecutionTimingStatistics> _statistics = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, RunningExecutionTimingInfo> _runningOperations = [];

    public IReadOnlyDictionary<string, ExecutionTimingStatistics> GetStatistics()
    {
        return new Dictionary<string, ExecutionTimingStatistics>(_statistics, StringComparer.Ordinal);
    }

    public ExecutionTimingStatistics? GetStatistics(string operationKey)
    {
        return _statistics.GetValueOrDefault(operationKey);
    }

    public IReadOnlyDictionary<Guid, RunningExecutionTimingInfo> GetRunningOperations()
    {
        return new Dictionary<Guid, RunningExecutionTimingInfo>(_runningOperations);
    }

    public void Reset(string operationKey)
    {
        _statistics.TryRemove(operationKey, out _);
    }

    internal void RegisterStart(
        Guid invocationId,
        string operationKey,
        string displayName,
        DateTimeOffset startedAt,
        string? description)
    {
        var running = new RunningExecutionTimingInfo(
            invocationId,
            operationKey,
            displayName,
            startedAt,
            description);

        if (!_runningOperations.TryAdd(invocationId, running))
        {
            throw new InvalidOperationException(
                $"Execution timing invocation '{invocationId}' is already running.");
        }
    }

    internal void CompleteRunning(Guid invocationId)
    {
        _runningOperations.TryRemove(invocationId, out _);
    }

    internal void ApplyRecord(ExecutionTimingCompletedSample sample)
    {
        _statistics.AddOrUpdate(
            sample.OperationKey,
            _ => new ExecutionTimingStatistics(
                sample.OperationKey,
                sample.DisplayName,
                1,
                sample.DurationMs,
                sample.RecordedAt)
            {
                AverageMemoryBytes = sample.MemoryBytes,
                LastMemoryBytes = sample.MemoryBytes,
                LastDurationMs = sample.DurationMs,
                LastRecordedAt = sample.RecordedAt
            },
            (_, current) =>
            {
                var nextCount = current.ExecutionCount + 1;
                var averageDuration = ((current.AverageDurationMs * current.ExecutionCount) + sample.DurationMs) / nextCount;
                long? averageMemory = sample.MemoryBytes is null
                    ? current.AverageMemoryBytes
                    : (long?)Math.Round((((current.AverageMemoryBytes ?? 0L) * current.ExecutionCount) + sample.MemoryBytes.Value) / (double)nextCount);

                return current with
                {
                    DisplayName = sample.DisplayName,
                    ExecutionCount = nextCount,
                    AverageDurationMs = averageDuration,
                    AverageMemoryBytes = averageMemory,
                    LastMemoryBytes = sample.MemoryBytes,
                    LastDurationMs = sample.DurationMs,
                    LastRecordedAt = sample.RecordedAt
                };
            });
    }
}
