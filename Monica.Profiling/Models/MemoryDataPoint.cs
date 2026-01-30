namespace Monica.Profiling.Models;

/// <summary>
///     内存数据点 - 用于实时监控和趋势图
/// </summary>
public class MemoryDataPoint
{
    /// <summary>
    ///     时间戳
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     GC 堆大小 (MB)
    /// </summary>
    public double GcHeapSizeMB { get; init; }

    /// <summary>
    ///     分配速率 (字节/秒)
    /// </summary>
    public double AllocationRateBps { get; init; }

    /// <summary>
    ///     Gen0 大小 (字节)
    /// </summary>
    public double Gen0SizeBytes { get; init; }

    /// <summary>
    ///     Gen1 大小 (字节)
    /// </summary>
    public double Gen1SizeBytes { get; init; }

    /// <summary>
    ///     Gen2 大小 (字节)
    /// </summary>
    public double Gen2SizeBytes { get; init; }

    /// <summary>
    ///     LOH (大对象堆) 大小 (字节)
    /// </summary>
    public double LohSizeBytes { get; init; }

    /// <summary>
    ///     POH (固定对象堆) 大小 (字节)
    /// </summary>
    public double PohSizeBytes { get; init; }

    /// <summary>
    ///     GC 时间占比 (%)
    /// </summary>
    public double TimeInGcPercent { get; init; }

    /// <summary>
    ///     GC 碎片化率 (%)
    /// </summary>
    public double GcFragmentation { get; init; }

    /// <summary>
    ///     工作集 (MB)
    /// </summary>
    public double WorkingSetMB { get; init; }

    /// <summary>
    ///     CPU 使用率 (%)
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    ///     线程数
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    ///     Gen0 回收次数 (增量)
    /// </summary>
    public double Gen0GcCount { get; init; }

    /// <summary>
    ///     Gen1 回收次数 (增量)
    /// </summary>
    public double Gen1GcCount { get; init; }

    /// <summary>
    ///     Gen2 回收次数 (增量)
    /// </summary>
    public double Gen2GcCount { get; init; }
}

/// <summary>
///     内存趋势数据
/// </summary>
public class MemoryTrendData
{
    /// <summary>
    ///     数据点列表
    /// </summary>
    public List<MemoryDataPoint> DataPoints { get; init; } = [];

    /// <summary>
    ///     最大历史点数
    /// </summary>
    public int MaxHistoryPoints { get; init; }

    /// <summary>
    ///     采样间隔 (毫秒)
    /// </summary>
    public int SampleIntervalMs { get; init; }

    /// <summary>
    ///     开始时间
    /// </summary>
    public DateTime? StartTime => DataPoints.Count > 0 ? DataPoints[0].Timestamp : null;

    /// <summary>
    ///     结束时间
    /// </summary>
    public DateTime? EndTime => DataPoints.Count > 0 ? DataPoints[^1].Timestamp : null;

    /// <summary>
    ///     平均堆大小 (MB)
    /// </summary>
    public double AverageHeapSizeMB => DataPoints.Count > 0
        ? DataPoints.Average(p => p.GcHeapSizeMB)
        : 0;

    /// <summary>
    ///     最大堆大小 (MB)
    /// </summary>
    public double MaxHeapSizeMB => DataPoints.Count > 0
        ? DataPoints.Max(p => p.GcHeapSizeMB)
        : 0;

    /// <summary>
    ///     平均分配速率 (字节/秒)
    /// </summary>
    public double AverageAllocationRate => DataPoints.Count > 0
        ? DataPoints.Average(p => p.AllocationRateBps)
        : 0;
}