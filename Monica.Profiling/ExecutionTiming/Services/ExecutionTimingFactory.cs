using Microsoft.Extensions.Logging;
using Monica.Profiling.ExecutionTiming.Abstractions;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Creates execution-timing recorders backed by the shared in-memory collector.
/// </summary>
internal sealed class ExecutionTimingFactory(
    ExecutionTimingCollector collector,
    ILogger<ExecutionTimingFactory> logger) : IExecutionTimingFactory
{
    public IExecutionTimingRecorder CreateRecorder(string name, string? description = null)
    {
        return new ExecutionTimingRecorder(name, description, logger, collector);
    }

    public IExecutionTimingRecorder BeginScope(string name, string? description = null)
    {
        return new ExecutionTimingScope(name, description, logger, collector);
    }
}
