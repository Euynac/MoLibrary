using Microsoft.Extensions.Logging;
using Monica.Profiling.ExecutionTiming.Abstractions.Internal;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Starts timing immediately and records the sample automatically when disposed.
/// </summary>
public sealed class ExecutionTimingScope : ExecutionTimingRecorder
{
    internal ExecutionTimingScope(
        string operationKey,
        string displayName,
        Guid? invocationId,
        string? description,
        ILogger logger,
        IExecutionTimingCoordinator coordinator)
        : base(
            operationKey,
            displayName,
            description,
            logger,
            coordinator,
            invocationId,
            stopOnDispose: true)
    {
        Start();
    }
}
