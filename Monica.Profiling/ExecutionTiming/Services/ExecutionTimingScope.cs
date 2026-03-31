using Microsoft.Extensions.Logging;

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
        ExecutionTimingCollector collector)
        : base(name, description, logger, collector, stopOnDispose: true)
    {
        Start();
    }
}
