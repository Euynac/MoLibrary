using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using MoLibrary.Profiling.Models;

namespace MoLibrary.Profiling.Services;

/// <summary>
///     内存指标收集器
///     使用 EventCounters 实时收集内存相关指标，并维护历史数据用于趋势分析
/// </summary>
public class MemoryMetricsCollector : EventListener, IDisposable
{
    private readonly ConcurrentDictionary<string, double> _counters = new();
    private readonly ConcurrentQueue<MemoryDataPoint> _history = new();
    private readonly int _maxHistoryPoints;
    private readonly Timer _snapshotTimer;
    private volatile bool _isDisposed;

    /// <summary>
    ///     初始化内存指标收集器
    /// </summary>
    /// <param name="maxHistoryPoints">最大历史数据点数 (默认 300 = 5 分钟)</param>
    /// <param name="sampleIntervalMs">采样间隔毫秒数 (默认 1000ms)</param>
    public MemoryMetricsCollector(int maxHistoryPoints = 300, int sampleIntervalMs = 1000)
    {
        _maxHistoryPoints = maxHistoryPoints;
        SampleIntervalMs = sampleIntervalMs;
        _snapshotTimer = new Timer(CaptureSnapshot, null, sampleIntervalMs, sampleIntervalMs);
    }

    /// <summary>
    ///     采样间隔 (毫秒)
    /// </summary>
    public int SampleIntervalMs { get; }

    /// <summary>
    ///     释放资源
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
    ///     当指标更新时触发
    /// </summary>
    public event Action<MemoryDataPoint>? OnMetricsUpdated;

    /// <summary>
    ///     当 EventSource 创建时被调用
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
    ///     当事件被写入时被调用
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
    ///     定时捕获快照
    /// </summary>
    private void CaptureSnapshot(object? state)
    {
        if (_isDisposed)
            return;

        var dataPoint = new MemoryDataPoint
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
            Gen2GcCount = GetCounter("gen-2-gc-count")
        };

        _history.Enqueue(dataPoint);

        // 维护最大历史点数
        while (_history.Count > _maxHistoryPoints) _history.TryDequeue(out _);

        OnMetricsUpdated?.Invoke(dataPoint);
    }

    /// <summary>
    ///     获取指定计数器的值
    /// </summary>
    private double GetCounter(string name)
    {
        return _counters.GetValueOrDefault(name);
    }

    /// <summary>
    ///     获取当前最新的数据点
    /// </summary>
    public MemoryDataPoint? GetCurrentDataPoint()
    {
        return _history.TryPeek(out var last) ? last : null;
    }

    /// <summary>
    ///     获取历史数据
    /// </summary>
    public MemoryTrendData GetTrendData()
    {
        return new MemoryTrendData
        {
            DataPoints = _history.ToList(),
            MaxHistoryPoints = _maxHistoryPoints,
            SampleIntervalMs = SampleIntervalMs
        };
    }

    /// <summary>
    ///     获取所有计数器的原始值
    /// </summary>
    public IReadOnlyDictionary<string, double> GetAllCounters()
    {
        return new Dictionary<string, double>(_counters);
    }
}