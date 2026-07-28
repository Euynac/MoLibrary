using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Services;

internal sealed class ExecutionTimingService(IExecutionTimingQuery executionTimingQuery)
{
    public IReadOnlyList<ExecutionTimingStatistics> GetStatistics()
    {
        return executionTimingQuery
            .GetStatistics()
            .Values
            .OrderByDescending(statistics => statistics.AverageDurationMs)
            .ThenBy(statistics => statistics.DisplayName, StringComparer.Ordinal)
            .ThenBy(statistics => statistics.OperationKey, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<RunningExecutionTimingInfo> GetRunningOperations()
    {
        return executionTimingQuery
            .GetRunningOperations()
            .Values
            .OrderByDescending(info => info.GetCurrentElapsedMs())
            .ThenBy(info => info.DisplayName, StringComparer.Ordinal)
            .ThenBy(info => info.InvocationId)
            .ToList();
    }
}
