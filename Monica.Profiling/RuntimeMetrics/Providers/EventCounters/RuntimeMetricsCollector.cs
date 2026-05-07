using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Globalization;
using Monica.Profiling.RuntimeMetrics.Models;

namespace Monica.Profiling.RuntimeMetrics.Providers.EventCounters;

/// <summary>
/// Collects runtime EventCounters and periodically snapshots them into a bounded in-memory history.
/// </summary>
internal sealed class RuntimeMetricsCollector : EventListener, IDisposable
{
    private const string SYSTEM_RUNTIME_EVENT_SOURCE_NAME = "System.Runtime";
    private const string EVENT_COUNTERS_EVENT_NAME = "EventCounters";
    private const string EVENT_COUNTER_INTERVAL_ARGUMENT = "EventCounterIntervalSec";
    private const string NAME_PAYLOAD_KEY = "Name";
    private const string MEAN_PAYLOAD_KEY = "Mean";
    private const string INCREMENT_PAYLOAD_KEY = "Increment";
    private const string COUNT_PAYLOAD_KEY = "Count";
    private const string GC_HEAP_SIZE_COUNTER_NAME = "gc-heap-size";
    private const string ALLOCATION_RATE_COUNTER_NAME = "alloc-rate";
    private const string GEN0_SIZE_COUNTER_NAME = "gen-0-size";
    private const string GEN1_SIZE_COUNTER_NAME = "gen-1-size";
    private const string GEN2_SIZE_COUNTER_NAME = "gen-2-size";
    private const string LOH_SIZE_COUNTER_NAME = "loh-size";
    private const string POH_SIZE_COUNTER_NAME = "poh-size";
    private const string TIME_IN_GC_COUNTER_NAME = "time-in-gc";
    private const string GC_FRAGMENTATION_COUNTER_NAME = "gc-fragmentation";
    private const string WORKING_SET_COUNTER_NAME = "working-set";
    private const string GEN0_GC_COUNT_COUNTER_NAME = "gen-0-gc-count";
    private const string GEN1_GC_COUNT_COUNTER_NAME = "gen-1-gc-count";
    private const string GEN2_GC_COUNT_COUNTER_NAME = "gen-2-gc-count";
    private const string CPU_USAGE_COUNTER_NAME = "cpu-usage";
    private const string THREADPOOL_THREAD_COUNT_COUNTER_NAME = "threadpool-thread-count";
    private const double BYTES_PER_MEGABYTE = 1_000_000d;

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
    /// Gets the sampling interval in milliseconds.
    /// </summary>
    public int SampleIntervalMs { get; }

    /// <summary>
    /// Releases runtime counter listener resources.
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
    /// Fires when a runtime metric point is captured.
    /// </summary>
    public event Action<RuntimeMetricsPoint>? MetricsUpdated;

    /// <summary>
    /// Called when an event source is created.
    /// </summary>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name != SYSTEM_RUNTIME_EVENT_SOURCE_NAME)
        {
            return;
        }

        EnableEvents(
            eventSource,
            EventLevel.Verbose,
            EventKeywords.All,
            new Dictionary<string, string?>
            {
                [EVENT_COUNTER_INTERVAL_ARGUMENT] = Math.Max(1, SampleIntervalMs / 1000d).ToString("G17", CultureInfo.InvariantCulture)
            });
    }

    /// <summary>
    /// Called when a runtime counter event is written.
    /// </summary>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName != EVENT_COUNTERS_EVENT_NAME || eventData.Payload == null)
        {
            return;
        }

        foreach (var payload in eventData.Payload)
        {
            if (payload is not IDictionary<string, object> eventPayload)
            {
                continue;
            }

            if (!eventPayload.TryGetValue(NAME_PAYLOAD_KEY, out var nameObj) || nameObj is not string name)
            {
                continue;
            }

            double? value = null;

            if (eventPayload.TryGetValue(MEAN_PAYLOAD_KEY, out var meanObj) &&
                TryParseCounterValue(meanObj, out var mean))
            {
                value = mean;
            }
            else if (eventPayload.TryGetValue(INCREMENT_PAYLOAD_KEY, out var incObj) &&
                     TryParseCounterValue(incObj, out var inc))
            {
                value = inc;
            }
            else if (eventPayload.TryGetValue(COUNT_PAYLOAD_KEY, out var countObj) &&
                     TryParseCounterValue(countObj, out var count))
            {
                value = count;
            }

            if (value.HasValue)
            {
                _counters[name] = value.Value;
            }
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
            GcHeapSizeBytes = MegabytesToBytes(GetCounter(GC_HEAP_SIZE_COUNTER_NAME)),
            AllocationRateBytesPerSecond = GetCounter(ALLOCATION_RATE_COUNTER_NAME),
            Gen0SizeBytes = GetCounter(GEN0_SIZE_COUNTER_NAME),
            Gen1SizeBytes = GetCounter(GEN1_SIZE_COUNTER_NAME),
            Gen2SizeBytes = GetCounter(GEN2_SIZE_COUNTER_NAME),
            LohSizeBytes = GetCounter(LOH_SIZE_COUNTER_NAME),
            PohSizeBytes = GetCounter(POH_SIZE_COUNTER_NAME),
            TimeInGcPercent = GetCounter(TIME_IN_GC_COUNTER_NAME),
            GcFragmentationPercent = GetCounter(GC_FRAGMENTATION_COUNTER_NAME),
            WorkingSetBytes = MegabytesToBytes(GetCounter(WORKING_SET_COUNTER_NAME)),
            Gen0GcCount = GetCounter(GEN0_GC_COUNT_COUNTER_NAME),
            Gen1GcCount = GetCounter(GEN1_GC_COUNT_COUNTER_NAME),
            Gen2GcCount = GetCounter(GEN2_GC_COUNT_COUNTER_NAME),
            CpuUsagePercent = GetCounter(CPU_USAGE_COUNTER_NAME),
            ThreadCount = (int)GetCounter(THREADPOOL_THREAD_COUNT_COUNTER_NAME)
        };

        _latestPoint = dataPoint;
        _history.Enqueue(dataPoint);

        while (_history.Count > _maxHistoryPoints)
        {
            _history.TryDequeue(out _);
        }

        MetricsUpdated?.Invoke(dataPoint);
    }

    /// <summary>
    /// Get the value of the specified counter
    /// </summary>
    private double GetCounter(string name)
    {
        return _counters.GetValueOrDefault(name);
    }

    private static double MegabytesToBytes(double megabytes)
    {
        return megabytes * BYTES_PER_MEGABYTE;
    }

    private static bool TryParseCounterValue(object? value, out double result)
    {
        return value switch
        {
            double typed => SetResult(typed, out result),
            float typed => SetResult(typed, out result),
            int typed => SetResult(typed, out result),
            long typed => SetResult(typed, out result),
            _ => double.TryParse(
                value?.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result)
        };
    }

    private static bool SetResult(double value, out double result)
    {
        result = value;
        return true;
    }

    /// <summary>
    /// Gets the latest data point.
    /// </summary>
    public RuntimeMetricsPoint? GetCurrentPoint() => _latestPoint;

    /// <summary>
    /// Gets historical data.
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
    /// Gets the raw EventCounter values as reported by System.Runtime.
    /// </summary>
    public IReadOnlyDictionary<string, double> GetAllCounters()
    {
        return new Dictionary<string, double>(_counters);
    }
}
