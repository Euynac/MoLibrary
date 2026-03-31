using System.Collections.Concurrent;
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Services;

internal sealed class ExecutionTimingCollector : IExecutionTimingQuery
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

    internal void Record(string name, long durationMs, string? description, long? memoryBytes)
    {
        var recordedAt = DateTimeOffset.UtcNow;
        _runningOperations.TryRemove(name, out _);

        _statistics.AddOrUpdate(
            name,
            _ => new ExecutionTimingStatistics(name, 1, durationMs, recordedAt)
            {
                AverageMemoryBytes = memoryBytes,
                LastMemoryBytes = memoryBytes,
                LastDurationMs = durationMs,
                LastRecordedAt = recordedAt
            },
            (_, current) =>
            {
                var nextCount = current.ExecutionCount + 1;
                var averageDuration = ((current.AverageDurationMs * current.ExecutionCount) + durationMs) / nextCount;
                long? averageMemory = memoryBytes is null
                    ? current.AverageMemoryBytes
                    : (long?)Math.Round((((current.AverageMemoryBytes ?? 0L) * current.ExecutionCount) + memoryBytes.Value) / (double)nextCount);

                return current with
                {
                    ExecutionCount = nextCount,
                    AverageDurationMs = averageDuration,
                    AverageMemoryBytes = averageMemory,
                    LastMemoryBytes = memoryBytes,
                    LastDurationMs = durationMs,
                    LastRecordedAt = recordedAt
                };
            });
    }
}
