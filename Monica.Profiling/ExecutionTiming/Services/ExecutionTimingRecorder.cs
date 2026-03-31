using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Monica.Profiling.ExecutionTiming.Abstractions;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Collects one or more timing samples for the same logical operation name.
/// </summary>
public class ExecutionTimingRecorder : IExecutionTimingRecorder
{
    private readonly ExecutionTimingCollector _collector;
    private readonly ILogger _logger;
    private readonly bool _stopOnDispose;
    private readonly Stopwatch _stopwatch = new();
    private readonly string _name;

    private bool _disposed;
    private long? _startedMemoryBytes;

    internal ExecutionTimingRecorder(
        string name,
        string? description,
        ILogger logger,
        ExecutionTimingCollector collector,
        bool stopOnDispose = false)
    {
        _name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Execution timing name cannot be null or whitespace.", nameof(name))
            : name;
        Description = description;
        _logger = logger;
        _collector = collector;
        _stopOnDispose = stopOnDispose;
    }

    /// <inheritdoc />
    public bool EnableLogging { get; set; }

    /// <inheritdoc />
    [Obsolete("Memory tracking relies on thread-local allocation counters and can be inaccurate across async continuations.")]
    public bool EnableMemoryTracking { get; set; }

    /// <inheritdoc />
    public string? Description { get; set; }

    /// <inheritdoc />
    public void Start()
    {
        ThrowIfDisposed();
        if (_stopwatch.IsRunning)
        {
            return;
        }

        _stopwatch.Start();
        _collector.RegisterStart(_name, DateTimeOffset.UtcNow, Description);

#pragma warning disable CS0618
        if (EnableMemoryTracking)
#pragma warning restore CS0618
        {
            _startedMemoryBytes = GC.GetAllocatedBytesForCurrentThread();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!_stopwatch.IsRunning)
        {
            return;
        }

        _stopwatch.Stop();
        var elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
        var memoryBytes = CaptureMemoryUsage();

        _collector.Record(_name, elapsedMilliseconds, Description, memoryBytes);

        if (EnableLogging)
        {
            _logger.LogInformation("{name} cost time: {time}", Description ?? _name, $"{elapsedMilliseconds}ms");
        }

        _stopwatch.Reset();
        _startedMemoryBytes = null;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_stopOnDispose)
        {
            Stop();
        }

        _disposed = true;
    }

    private long? CaptureMemoryUsage()
    {
#pragma warning disable CS0618
        if (!EnableMemoryTracking)
#pragma warning restore CS0618
        {
            return null;
        }

        var startedMemoryBytes = _startedMemoryBytes;
        if (!startedMemoryBytes.HasValue)
        {
            return null;
        }

        var currentMemoryBytes = GC.GetAllocatedBytesForCurrentThread();
        var delta = currentMemoryBytes - startedMemoryBytes.Value;
        return delta >= 0 ? delta : null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }
}
