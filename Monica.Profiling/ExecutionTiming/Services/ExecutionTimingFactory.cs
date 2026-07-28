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
        return new ExecutionTimingRecorder(name, name, description, logger, coordinator);
    }

    public IExecutionTimingRecorder BeginScope(string name, string? description = null)
    {
        return new ExecutionTimingScope(name, name, invocationId: null, description, logger, coordinator);
    }

    public IExecutionTimingRecorder BeginInvocation(
        string operationKey,
        string displayName,
        Guid invocationId,
        string? description = null)
    {
        if (invocationId == Guid.Empty)
        {
            throw new ArgumentException("Execution timing invocation IDs cannot be empty.", nameof(invocationId));
        }

        return new ExecutionTimingScope(
            operationKey,
            displayName,
            invocationId,
            description,
            logger,
            coordinator);
    }
}
