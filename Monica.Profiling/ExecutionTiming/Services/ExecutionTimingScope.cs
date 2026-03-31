using Microsoft.Extensions.Logging;
using Monica.Profiling.ExecutionTiming.Abstractions.Internal;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Starts timing immediately and records the sample automatically when disposed.
/// </summary>
public sealed class ExecutionTimingScope : ExecutionTimingRecorder
{
    internal ExecutionTimingScope(
        string name,
        string? description,
        ILogger logger,
        IExecutionTimingCoordinator coordinator)
        : base(name, description, logger, coordinator, stopOnDispose: true)
    {
        Start();
    }
}
