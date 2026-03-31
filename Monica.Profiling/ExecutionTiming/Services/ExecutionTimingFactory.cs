using Microsoft.Extensions.Logging;
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Abstractions.Internal;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Creates execution-timing recorders backed by the shared in-memory collector.
/// </summary>
internal sealed class ExecutionTimingFactory(
    IExecutionTimingCoordinator coordinator,
    ILogger<ExecutionTimingFactory> logger) : IExecutionTimingFactory
{
    public IExecutionTimingRecorder CreateRecorder(string name, string? description = null)
    {
        return new ExecutionTimingRecorder(name, description, logger, coordinator);
    }

    public IExecutionTimingRecorder BeginScope(string name, string? description = null)
    {
        return new ExecutionTimingScope(name, description, logger, coordinator);
    }
}
