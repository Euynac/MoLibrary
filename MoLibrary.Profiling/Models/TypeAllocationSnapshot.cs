namespace MoLibrary.Profiling.Models;

/// <summary>
///     类型分配快照 - 某一时刻的类型分配数据
/// </summary>
public class TypeAllocationSnapshot
{
    /// <summary>
    ///     快照时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     收集持续时间 (从收集开始或上次重置算起)
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    ///     所有跟踪的类型及其分配信息，按 TotalBytes 降序排列
    /// </summary>
    public IReadOnlyList<TypeAllocationInfo> Types { get; init; } = [];

    /// <summary>
    ///     所有类型的总分配数
    /// </summary>
    public long TotalAllocationCount { get; init; }

    /// <summary>
    ///     所有类型的总字节数
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    ///     唯一类型数��
    /// </summary>
    public int UniqueTypeCount => Types.Count;

    /// <summary>
    ///     是否正在收集数据
    /// </summary>
    public bool IsCollecting { get; init; }

    /// <summary>
    ///     当前采样模式
    /// </summary>
    public AllocationSamplingMode SamplingMode { get; init; }

    /// <summary>
    ///     丢弃的事件数量 (例如由于未知类型)
    /// </summary>
    public long DroppedEventCount { get; init; }

    /// <summary>
    ///     数据来源类型
    /// </summary>
    public TypeAllocationDataSource DataSource { get; init; }

    /// <summary>
    ///     错误消息 (如果有)
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
///     分配采样模式
/// </summary>
public enum AllocationSamplingMode
{
    /// <summary>
    ///     收集已禁用
    /// </summary>
    Disabled = 0,

    /// <summary>
    ///     低频采样 (~5 事件/秒) - 较低开销
    /// </summary>
    Low = 1,

    /// <summary>
    ///     高频采样 (~100 事件/秒) - 较高精度
    /// </summary>
    High = 2
}

/// <summary>
///     类型分配数据来源
/// </summary>
public enum TypeAllocationDataSource
{
    /// <summary>
    ///     无数据
    /// </summary>
    None = 0,

    /// <summary>
    ///     实时分配跟踪 (ETW/EventPipe)
    /// </summary>
    AllocationTracking = 1,

    /// <summary>
    ///     堆快照 (ClrMD)
    /// </summary>
    HeapSnapshot = 2
}
