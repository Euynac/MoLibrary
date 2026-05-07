namespace Monica.Profiling.RuntimeMetrics.Models;

/// <summary>
/// Runtime metric sample used for real-time monitoring and trending.
/// </summary>
public class RuntimeMetricsPoint
{
    /// <summary>
    /// Gets the UTC timestamp when this runtime sample was captured.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the GC heap size in bytes.
    /// </summary>
    public double GcHeapSizeBytes { get; init; }

    /// <summary>
    /// Gets the allocation rate in bytes per second.
    /// </summary>
    public double AllocationRateBytesPerSecond { get; init; }

    /// <summary>
    /// Gets the Gen 0 heap size in bytes.
    /// </summary>
    public double Gen0SizeBytes { get; init; }

    /// <summary>
    /// Gets the Gen 1 heap size in bytes.
    /// </summary>
    public double Gen1SizeBytes { get; init; }

    /// <summary>
    /// Gets the Gen 2 heap size in bytes.
    /// </summary>
    public double Gen2SizeBytes { get; init; }

    /// <summary>
    /// Gets the large object heap size in bytes.
    /// </summary>
    public double LohSizeBytes { get; init; }

    /// <summary>
    /// Gets the pinned object heap size in bytes.
    /// </summary>
    public double PohSizeBytes { get; init; }

    /// <summary>
    /// Gets the percentage of time spent in GC since the last GC.
    /// </summary>
    public double TimeInGcPercent { get; init; }

    /// <summary>
    /// Gets the GC fragmentation percentage.
    /// </summary>
    public double GcFragmentationPercent { get; init; }

    /// <summary>
    /// Gets the process working set in bytes.
    /// </summary>
    public double WorkingSetBytes { get; init; }

    /// <summary>
    /// Gets the process CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets the ThreadPool thread count.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the Gen 0 GC count increment for the sample interval.
    /// </summary>
    public double Gen0GcCount { get; init; }

    /// <summary>
    /// Gets the Gen 1 GC count increment for the sample interval.
    /// </summary>
    public double Gen1GcCount { get; init; }

    /// <summary>
    /// Gets the Gen 2 GC count increment for the sample interval.
    /// </summary>
    public double Gen2GcCount { get; init; }

    /// <summary>
    /// Gets the GC heap size in megabytes for UI compatibility.
    /// </summary>
    public double GcHeapSizeMB => GcHeapSizeBytes / 1_000_000d;

    /// <summary>
    /// Gets the allocation rate in bytes per second for UI compatibility.
    /// </summary>
    public double AllocationRateBps => AllocationRateBytesPerSecond;

    /// <summary>
    /// Gets the GC fragmentation percentage for UI compatibility.
    /// </summary>
    public double GcFragmentation => GcFragmentationPercent;

    /// <summary>
    /// Gets the process working set in megabytes for UI compatibility.
    /// </summary>
    public double WorkingSetMB => WorkingSetBytes / 1_000_000d;
}

/// <summary>
/// Runtime metrics trend retained in memory.
/// </summary>
public class RuntimeMetricsTrend
{
    /// <summary>
    /// Gets retained data points.
    /// </summary>
    public List<RuntimeMetricsPoint> DataPoints { get; init; } = [];

    /// <summary>
    /// Gets the maximum historical points retained by the collector.
    /// </summary>
    public int MaxHistoryPoints { get; init; }

    /// <summary>
    /// Gets the sampling interval in milliseconds.
    /// </summary>
    public int SampleIntervalMs { get; init; }

    /// <summary>
    /// Gets the first retained sample timestamp.
    /// </summary>
    public DateTime? StartTime => DataPoints.Count > 0 ? DataPoints[0].Timestamp : null;

    /// <summary>
    /// Gets the last retained sample timestamp.
    /// </summary>
    public DateTime? EndTime => DataPoints.Count > 0 ? DataPoints[^1].Timestamp : null;

    /// <summary>
    /// Gets the average GC heap size in mebibytes.
    /// </summary>
    public double AverageHeapSizeMB => DataPoints.Count > 0
        ? DataPoints.Average(p => p.GcHeapSizeMB)
        : 0;

    /// <summary>
    /// Gets the maximum GC heap size in mebibytes.
    /// </summary>
    public double MaxHeapSizeMB => DataPoints.Count > 0
        ? DataPoints.Max(p => p.GcHeapSizeMB)
        : 0;

    /// <summary>
    /// Gets the average allocation rate in bytes per second.
    /// </summary>
    public double AverageAllocationRate => DataPoints.Count > 0
        ? DataPoints.Average(p => p.AllocationRateBytesPerSecond)
        : 0;
}
