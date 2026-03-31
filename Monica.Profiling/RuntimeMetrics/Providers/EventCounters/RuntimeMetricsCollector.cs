using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Monica.Profiling.RuntimeMetrics.Models;

namespace Monica.Profiling.RuntimeMetrics.Providers.EventCounters;

/// <summary>
/// Collects runtime EventCounters and periodically snapshots them into a bounded in-memory history.
/// </summary>
internal sealed class RuntimeMetricsCollector : EventListener, IDisposable
{
    private readonly ConcurrentDictionary<string, double> _counters = new();
    private readonly ConcurrentQueue<RuntimeMetricsPoint> _history = new();
    private readonly int _maxHistoryPoints;
    private readonly Timer _snapshotTimer;
    private volatile bool _isDisposed;
    private volatile RuntimeMetricsPoint? _latestPoint;

    /// <summary>
    /// Initialize the performance indicator collector
    /// </summary>
    /// <param name="maxHistoryPoints">Maximum number of historical data points (default 300 = 5 minutes)</param>
    /// <param name="sampleIntervalMs">Sampling interval in milliseconds (default 1000ms)</param>
    public RuntimeMetricsCollector(int maxHistoryPoints = 300, int sampleIntervalMs = 1000)
    {
        _maxHistoryPoints = maxHistoryPoints;
        SampleIntervalMs = sampleIntervalMs;
        _snapshotTimer = new Timer(CaptureSnapshot, null, sampleIntervalMs, sampleIntervalMs);
    }

    /// <summary>
    /// Sampling interval (milliseconds)
    /// </summary>
    public int SampleIntervalMs { get; }

    /// <summary>
    /// Release resources
    /// </summary>
    public override void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _snapshotTimer.Dispose();
        _counters.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Fires when the indicator is updated
    /// </summary>
    public event Action<RuntimeMetricsPoint>? MetricsUpdated;

    /// <summary>
    /// Called when the EventSource is created
    /// </summary>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Runtime")
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All,
                new Dictionary<string, string?>
                {
                    ["EventCounterIntervalSec"] = "1"
                });
    }

    /// <summary>
    /// Called when an event is written
    /// </summary>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName != "EventCounters" || eventData.Payload == null)
            return;

        foreach (var payload in eventData.Payload)
        {
            if (payload is not IDictionary<string, object> eventPayload)
                continue;

            if (!eventPayload.TryGetValue("Name", out var nameObj) || nameObj is not string name)
                continue;

            double? value = null;

            if (eventPayload.TryGetValue("Mean", out var meanObj) &&
                double.TryParse(meanObj?.ToString(), out var mean))
                value = mean;
            else if (eventPayload.TryGetValue("Increment", out var incObj) &&
                     double.TryParse(incObj?.ToString(), out var inc))
                value = inc;
            else if (eventPayload.TryGetValue("Count", out var countObj) &&
                     double.TryParse(countObj?.ToString(), out var count))
                value = count;

            if (value.HasValue) _counters[name] = value.Value;
        }
    }

    /// <summary>
    /// Capture snapshots regularly
    /// </summary>
    private void CaptureSnapshot(object? state)
    {
        if (_isDisposed)
            return;

        var dataPoint = new RuntimeMetricsPoint
        {
            Timestamp = DateTime.UtcNow,
            GcHeapSizeMB = GetCounter("gc-heap-size"),
            AllocationRateBps = GetCounter("alloc-rate"),
            Gen0SizeBytes = GetCounter("gen-0-size"),
            Gen1SizeBytes = GetCounter("gen-1-size"),
            Gen2SizeBytes = GetCounter("gen-2-size"),
            LohSizeBytes = GetCounter("loh-size"),
            PohSizeBytes = GetCounter("poh-size"),
            TimeInGcPercent = GetCounter("time-in-gc"),
            GcFragmentation = GetCounter("gc-fragmentation"),
            WorkingSetMB = GetCounter("working-set"),
            Gen0GcCount = GetCounter("gen-0-gc-count"),
            Gen1GcCount = GetCounter("gen-1-gc-count"),
            Gen2GcCount = GetCounter("gen-2-gc-count"),
            CpuUsagePercent = GetCounter("cpu-usage"),
            ThreadCount = (int)GetCounter("threadpool-thread-count")
        };

        _latestPoint = dataPoint;
        _history.Enqueue(dataPoint);

        // Maintain maximum historical points
        while (_history.Count > _maxHistoryPoints) _history.TryDequeue(out _);

        MetricsUpdated?.Invoke(dataPoint);
    }

    /// <summary>
    /// Get the value of the specified counter
    /// </summary>
    private double GetCounter(string name)
    {
        return _counters.GetValueOrDefault(name);
    }

    /// <summary>
    /// Get the latest data point
    /// </summary>
    public RuntimeMetricsPoint? GetCurrentPoint() => _latestPoint;

    /// <summary>
    /// Get historical data
    /// </summary>
    public RuntimeMetricsTrend GetTrend()
    {
        return new RuntimeMetricsTrend
        {
            DataPoints = _history.ToList(),
            MaxHistoryPoints = _maxHistoryPoints,
            SampleIntervalMs = SampleIntervalMs
        };
    }

    /// <summary>
    /// Get the raw values ​​of all counters
    /// </summary>
    public IReadOnlyDictionary<string, double> GetAllCounters()
    {
        return new Dictionary<string, double>(_counters);
    }
}
