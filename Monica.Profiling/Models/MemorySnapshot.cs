using System.Runtime;

namespace Monica.Profiling.Models;

/// <summary>
///     内存快照 - 某一时刻的内存状态
/// </summary>
public class MemorySnapshot
{
    /// <summary>
    ///     快照时间戳
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     工作集大小 (字节)
    /// </summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>
    ///     托管内存总量 (字节)
    /// </summary>
    public long TotalManagedMemory { get; init; }

    /// <summary>
    ///     GC 堆大小 (字节)
    /// </summary>
    public long GcHeapSizeBytes { get; init; }

    /// <summary>
    ///     已提交内存 (字节)
    /// </summary>
    public long CommittedBytes { get; init; }

    /// <summary>
    ///     Gen0 回收次数
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    ///     Gen1 回收次数
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    ///     Gen2 回收次数
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    ///     是否为服务器 GC 模式
    /// </summary>
    public bool IsServerGC { get; init; }

    /// <summary>
    ///     GC 延迟模式
    /// </summary>
    public GCLatencyMode LatencyMode { get; init; }

    /// <summary>
    ///     GC 延迟模式显示名称
    /// </summary>
    public string LatencyModeDisplayName => LatencyMode switch
    {
        GCLatencyMode.Batch => "批量模式",
        GCLatencyMode.Interactive => "交互模式",
        GCLatencyMode.LowLatency => "低延迟模式",
        GCLatencyMode.SustainedLowLatency => "持续低延迟模式",
        GCLatencyMode.NoGCRegion => "无GC区域",
        _ => LatencyMode.ToString()
    };
}