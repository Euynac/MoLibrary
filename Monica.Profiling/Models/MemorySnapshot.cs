using System.Runtime;

namespace Monica.Profiling.Models;

/// <summary>
/// Memory snapshot - memory state at a certain moment
/// </summary>
public class MemorySnapshot
{
    /// <summary>
    /// snapshot timestamp
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Working set size (bytes)
    /// </summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>
    /// Total managed memory (bytes)
    /// </summary>
    public long TotalManagedMemory { get; init; }

    /// <summary>
    /// GC heap size (bytes)
    /// </summary>
    public long GcHeapSizeBytes { get; init; }

    /// <summary>
    /// Committed memory (bytes)
    /// </summary>
    public long CommittedBytes { get; init; }

    /// <summary>
    /// Gen0 recycling times
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Gen1 recycling times
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Gen2 recycling times
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    /// Whether it is server GC mode
    /// </summary>
    public bool IsServerGC { get; init; }

    /// <summary>
    /// GC delay mode
    /// </summary>
    public GCLatencyMode LatencyMode { get; init; }

    /// <summary>
    /// GC delay mode display name
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