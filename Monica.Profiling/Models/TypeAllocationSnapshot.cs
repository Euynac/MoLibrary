namespace Monica.Profiling.Models;

/// <summary>
/// Type allocation snapshot - type allocation data at a certain point in time
/// </summary>
public class TypeAllocationSnapshot
{
    /// <summary>
    /// snapshot timestamp
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Collection duration (measured from start of collection or last reset)
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// All tracked types and their allocation information, sorted by TotalBytes in descending order
    /// </summary>
    public IReadOnlyList<TypeAllocationInfo> Types { get; init; } = [];

    /// <summary>
    /// Total allocations for all types
    /// </summary>
    public long TotalAllocationCount { get; init; }

    /// <summary>
    /// Total bytes of all types
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Unique type number ��
    /// </summary>
    public int UniqueTypeCount => Types.Count;

    /// <summary>
    /// Whether data is being collected
    /// </summary>
    public bool IsCollecting { get; init; }

    /// <summary>
    /// Current sampling mode
    /// </summary>
    public AllocationSamplingMode SamplingMode { get; init; }

    /// <summary>
    /// Number of events dropped (e.g. due to unknown type)
    /// </summary>
    public long DroppedEventCount { get; init; }

    /// <summary>
    /// Data source type
    /// </summary>
    public TypeAllocationDataSource DataSource { get; init; }

    /// <summary>
    /// Error message (if any)
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Assign sampling mode
/// </summary>
public enum AllocationSamplingMode
{
    /// <summary>
    /// Collection is disabled
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Low frequency sampling (~5 events/second) - lower overhead
    /// </summary>
    Low = 1,

    /// <summary>
    /// High frequency sampling (~100 events/second) - higher accuracy
    /// </summary>
    High = 2
}

/// <summary>
/// Type assignment data source
/// </summary>
public enum TypeAllocationDataSource
{
    /// <summary>
    /// No data
    /// </summary>
    None = 0,

    /// <summary>
    /// Real-time allocation tracking (ETW/EventPipe)
    /// </summary>
    AllocationTracking = 1,

    /// <summary>
    /// Heap snapshot (ClrMD)
    /// </summary>
    HeapSnapshot = 2
}
