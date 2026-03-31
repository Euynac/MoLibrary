using System.Collections.Concurrent;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Services;

internal sealed class ExecutionTimingCollector
{
    private readonly ConcurrentDictionary<string, ExecutionTimingStatistics> _statistics = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RunningExecutionTimingInfo> _runningOperations = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ExecutionTimingStatistics> GetStatistics()
    {
        return new Dictionary<string, ExecutionTimingStatistics>(_statistics, StringComparer.Ordinal);
    }

    public ExecutionTimingStatistics? GetStatistics(string name)
    {
        return _statistics.GetValueOrDefault(name);
    }

    public IReadOnlyDictionary<string, RunningExecutionTimingInfo> GetRunningOperations()
    {
        return new Dictionary<string, RunningExecutionTimingInfo>(_runningOperations, StringComparer.Ordinal);
    }

    public void Reset(string name)
    {
        _statistics.TryRemove(name, out _);
    }

    internal void RegisterStart(string name, DateTimeOffset startedAt, string? description)
    {
        _runningOperations.AddOrUpdate(
            name,
            _ => new RunningExecutionTimingInfo(name, startedAt, description),
            (_, _) => new RunningExecutionTimingInfo(name, startedAt, description));
    }

    internal void CompleteRunning(string name)
    {
        _runningOperations.TryRemove(name, out _);
    }

    internal void ApplyRecord(ExecutionTimingCompletedSample sample)
    {
        _statistics.AddOrUpdate(
            sample.Name,
            _ => new ExecutionTimingStatistics(sample.Name, 1, sample.DurationMs, sample.RecordedAt)
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
