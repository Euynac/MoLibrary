namespace MoLibrary.Profiling.Models;

/// <summary>
///     详细 GC 信息 - 基于 GCMemoryInfo
/// </summary>
public class DetailedGCInfo
{
    /// <summary>
    ///     GC 索引 (从 1 开始)
    /// </summary>
    public long GCIndex { get; init; }

    /// <summary>
    ///     触发的代数 (0, 1, 或 2)
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    ///     是否为压缩 GC
    /// </summary>
    public bool WasCompacting { get; init; }

    /// <summary>
    ///     是否为并发 GC
    /// </summary>
    public bool WasConcurrent { get; init; }

    /// <summary>
    ///     堆大小 (字节)
    /// </summary>
    public long HeapSizeBytes { get; init; }

    /// <summary>
    ///     碎片化字节数
    /// </summary>
    public long FragmentedBytes { get; init; }

    /// <summary>
    ///     碎片化百分比
    /// </summary>
    public double FragmentationPercent => HeapSizeBytes > 0
        ? (double) FragmentedBytes / HeapSizeBytes * 100
        : 0;

    /// <summary>
    ///     已提交内存 (字节)
    /// </summary>
    public long CommittedBytes { get; init; }

    /// <summary>
    ///     提升的字节数 (从低代提升到高代)
    /// </summary>
    public long PromotedBytes { get; init; }

    /// <summary>
    ///     固定对象数量
    /// </summary>
    public long PinnedObjectsCount { get; init; }

    /// <summary>
    ///     待终结队列中的对象数量
    /// </summary>
    public long FinalizationPendingCount { get; init; }

    /// <summary>
    ///     GC 暂停时间
    /// </summary>
    public TimeSpan PauseDuration { get; init; }

    /// <summary>
    ///     GC 时间占比
    /// </summary>
    public double PauseTimePercentage { get; init; }

    /// <summary>
    ///     高内存负载阈值 (字节)
    /// </summary>
    public long HighMemoryLoadThresholdBytes { get; init; }

    /// <summary>
    ///     当前内存负载 (字节)
    /// </summary>
    public long MemoryLoadBytes { get; init; }

    /// <summary>
    ///     总可用内存 (字节)
    /// </summary>
    public long TotalAvailableMemoryBytes { get; init; }

    /// <summary>
    ///     各代详细信息
    /// </summary>
    public GenerationDetailInfo[] GenerationDetails { get; init; } = [];
}

/// <summary>
///     单个代的详细信息
/// </summary>
public class GenerationDetailInfo
{
    /// <summary>
    ///     代数 (0, 1, 2, 3=LOH, 4=POH)
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    ///     代数显示名称
    /// </summary>
    public string GenerationName => Generation switch
    {
        0 => "Gen 0",
        1 => "Gen 1",
        2 => "Gen 2",
        3 => "LOH",
        4 => "POH",
        _ => $"Gen {Generation}"
    };

    /// <summary>
    ///     GC 前大小 (字节)
    /// </summary>
    public long SizeBeforeBytes { get; init; }

    /// <summary>
    ///     GC 后大小 (字节)
    /// </summary>
    public long SizeAfterBytes { get; init; }

    /// <summary>
    ///     GC 前碎片化 (字节)
    /// </summary>
    public long FragmentationBeforeBytes { get; init; }

    /// <summary>
    ///     GC 后碎片化 (字节)
    /// </summary>
    public long FragmentationAfterBytes { get; init; }

    /// <summary>
    ///     回收的字节数
    /// </summary>
    public long CollectedBytes => SizeBeforeBytes - SizeAfterBytes;
}