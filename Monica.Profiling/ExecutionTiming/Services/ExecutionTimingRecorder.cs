using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Abstractions.Internal;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Collects one or more timing samples for the same stable operation identity.
/// </summary>
public class ExecutionTimingRecorder : IExecutionTimingRecorder
{
    private readonly IExecutionTimingCoordinator _coordinator;
    private readonly ILogger _logger;
    private readonly bool _stopOnDispose;
    private readonly Stopwatch _stopwatch = new();
    private readonly string _displayName;
    private readonly string _operationKey;

    private Guid? _activeInvocationId;
    private bool _disposed;
    private Guid? _initialInvocationId;
    private long? _startedMemoryBytes;

    internal ExecutionTimingRecorder(
        string operationKey,
        string displayName,
        string? description,
        ILogger logger,
        IExecutionTimingCoordinator coordinator,
        Guid? invocationId = null,
        bool stopOnDispose = false)
    {
        _operationKey = ValidateIdentity(operationKey, nameof(operationKey), "operation key");
        _displayName = ValidateIdentity(displayName, nameof(displayName), "display name");
        Description = description;
        _logger = logger;
        _coordinator = coordinator;
        _initialInvocationId = invocationId;
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

        var invocationId = _initialInvocationId ?? Guid.NewGuid();
        _initialInvocationId = null;
        _coordinator.RegisterStart(
            invocationId,
            _operationKey,
            _displayName,
            DateTimeOffset.UtcNow,
            Description);
        _activeInvocationId = invocationId;
        _stopwatch.Start();

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
        var invocationId = _activeInvocationId
            ?? throw new InvalidOperationException("A running execution timing sample has no invocation identity.");

        _coordinator.CompleteSample(
            invocationId,
            _operationKey,
            _displayName,
            elapsedMilliseconds,
            memoryBytes);

        if (EnableLogging)
        {
            _logger.LogInformation(
                "{name} cost time: {time}",
                Description ?? _displayName,
                $"{elapsedMilliseconds}ms");
        }

        _stopwatch.Reset();
        _activeInvocationId = null;
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

    private static string ValidateIdentity(string value, string parameterName, string identityKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Execution timing {identityKind} cannot contain leading or trailing whitespace.",
                parameterName);
        }

        return value;
    }
}
