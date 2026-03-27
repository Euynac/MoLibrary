namespace Monica.Profiling.Models;

/// <summary>
/// In-memory data points - for real-time monitoring and trending
/// </summary>
public class MemoryDataPoint
{
    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// GC heap size (MB)
    /// </summary>
    public double GcHeapSizeMB { get; init; }

    /// <summary>
    /// Allocation rate (bytes/second)
    /// </summary>
    public double AllocationRateBps { get; init; }

    /// <summary>
    /// Gen0 size (bytes)
    /// </summary>
    public double Gen0SizeBytes { get; init; }

    /// <summary>
    /// Gen1 size (bytes)
    /// </summary>
    public double Gen1SizeBytes { get; init; }

    /// <summary>
    /// Gen2 size (bytes)
    /// </summary>
    public double Gen2SizeBytes { get; init; }

    /// <summary>
    /// LOH (Large Object Heap) size (bytes)
    /// </summary>
    public double LohSizeBytes { get; init; }

    /// <summary>
    /// POH (Pinned Object Heap) size (bytes)
    /// </summary>
    public double PohSizeBytes { get; init; }

    /// <summary>
    /// GC time proportion (%)
    /// </summary>
    public double TimeInGcPercent { get; init; }

    /// <summary>
    /// GC fragmentation rate (%)
    /// </summary>
    public double GcFragmentation { get; init; }

    /// <summary>
    /// Working set (MB)
    /// </summary>
    public double WorkingSetMB { get; init; }

    /// <summary>
    /// CPU usage (%)
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Number of threads
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gen0 recycling times (increment)
    /// </summary>
    public double Gen0GcCount { get; init; }

    /// <summary>
    /// Gen1 recycling times (increment)
    /// </summary>
    public double Gen1GcCount { get; init; }

    /// <summary>
    /// Gen2 recycling times (incremental)
    /// </summary>
    public double Gen2GcCount { get; init; }
}

/// <summary>
/// Memory trend data
/// </summary>
public class MemoryTrendData
{
    /// <summary>
    /// List of data points
    /// </summary>
    public List<MemoryDataPoint> DataPoints { get; init; } = [];

    /// <summary>
    /// Maximum historical points
    /// </summary>
    public int MaxHistoryPoints { get; init; }

    /// <summary>
    /// Sampling interval (milliseconds)
    /// </summary>
    public int SampleIntervalMs { get; init; }

    /// <summary>
    /// start time
    /// </summary>
    public DateTime? StartTime => DataPoints.Count > 0 ? DataPoints[0].Timestamp : null;

    /// <summary>
    /// end time
    /// </summary>
    public DateTime? EndTime => DataPoints.Count > 0 ? DataPoints[^1].Timestamp : null;

    /// <summary>
    /// Average heap size (MB)
    /// </summary>
    public double AverageHeapSizeMB => DataPoints.Count > 0
        ? DataPoints.Average(p => p.GcHeapSizeMB)
        : 0;

    /// <summary>
    /// Maximum heap size (MB)
    /// </summary>
    public double MaxHeapSizeMB => DataPoints.Count > 0
        ? DataPoints.Max(p => p.GcHeapSizeMB)
        : 0;

    /// <summary>
    /// Average allocation rate (bytes/second)
    /// </summary>
    public double AverageAllocationRate => DataPoints.Count > 0
        ? DataPoints.Average(p => p.AllocationRateBps)
        : 0;
}