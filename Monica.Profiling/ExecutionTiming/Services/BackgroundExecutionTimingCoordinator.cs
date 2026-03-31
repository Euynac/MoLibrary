using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;
using Monica.Profiling.ExecutionTiming.Abstractions.Internal;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.ExecutionTiming.Services;

/// <summary>
/// Aggregates execution-timing samples on a background service to reduce hot-path write cost.
/// </summary>
internal sealed class BackgroundExecutionTimingCoordinator(
    ExecutionTimingCollector collector,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleExecutionTimingOption> executionTimingOptions,
    ILogger<BackgroundExecutionTimingCoordinator> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger), IExecutionTimingCoordinator
{
    private static readonly TimeSpan _defaultFlushInterval = TimeSpan.FromMilliseconds(250);

    private readonly ConcurrentQueue<ExecutionTimingCompletedSample> _pendingSamples = [];
    private readonly Lock _flushSync = new();
    private readonly ModuleExecutionTimingOption _options = executionTimingOptions.Value;

    public override string ServiceName => "ExecutionTimingBatchAggregator";

    public override TimeSpan? HeartbeatInterval => null;

    public IReadOnlyDictionary<string, ExecutionTimingStatistics> GetStatistics()
    {
        FlushPendingSamples();
        return collector.GetStatistics();
    }

    public ExecutionTimingStatistics? GetStatistics(string name)
    {
        FlushPendingSamples();
        return collector.GetStatistics(name);
    }

    public IReadOnlyDictionary<string, RunningExecutionTimingInfo> GetRunningOperations()
    {
        return collector.GetRunningOperations();
    }

    public void Reset(string name)
    {
        FlushPendingSamples();
        collector.Reset(name);
    }

    public void RegisterStart(string name, DateTimeOffset startedAt, string? description)
    {
        collector.RegisterStart(name, startedAt, description);
    }

    public void CompleteSample(string name, long durationMs, string? description, long? memoryBytes)
    {
        collector.CompleteRunning(name);
        _pendingSamples.Enqueue(new ExecutionTimingCompletedSample(
            name,
            durationMs,
            DateTimeOffset.UtcNow,
            memoryBytes));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        FlushPendingSamples();
        await base.StopAsync(cancellationToken);
        FlushPendingSamples();
    }

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        var flushInterval = GetFlushInterval();

        RecordState(
            $"Execution timing background aggregation enabled with flush interval {flushInterval.TotalMilliseconds:0}ms",
            logLevel: LogLevel.Information);

        using var timer = new PeriodicTimer(flushInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            FlushPendingSamples();
        }
    }

    private void FlushPendingSamples()
    {
        if (_pendingSamples.IsEmpty)
        {
            return;
        }

        lock (_flushSync)
        {
            while (_pendingSamples.TryDequeue(out var sample))
            {
                collector.ApplyRecord(sample);
            }
        }
    }

    private TimeSpan GetFlushInterval()
    {
        return _options.BackgroundFlushInterval > TimeSpan.Zero
            ? _options.BackgroundFlushInterval
            : _defaultFlushInterval;
    }
}
