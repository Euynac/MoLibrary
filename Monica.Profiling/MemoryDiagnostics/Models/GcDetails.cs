namespace Monica.Profiling.MemoryDiagnostics.Models;

/// <summary>
/// Detailed GC information - based on GCMemoryInfo
/// </summary>
public class GcDetails
{
    /// <summary>
    /// GC index (starting from 1)
    /// </summary>
    public long GCIndex { get; init; }

    /// <summary>
    /// Number of generations to trigger (0, 1, or 2)
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    /// Is it a compact GC?
    /// </summary>
    public bool WasCompacting { get; init; }

    /// <summary>
    /// Whether it is concurrent GC
    /// </summary>
    public bool WasConcurrent { get; init; }

    /// <summary>
    /// Heap size (bytes)
    /// </summary>
    public long HeapSizeBytes { get; init; }

    /// <summary>
    /// Number of fragmented bytes
    /// </summary>
    public long FragmentedBytes { get; init; }

    /// <summary>
    /// fragmentation percentage
    /// </summary>
    public double FragmentationPercent => HeapSizeBytes > 0
        ? (double) FragmentedBytes / HeapSizeBytes * 100
        : 0;

    /// <summary>
    /// Committed memory (bytes)
    /// </summary>
    public long CommittedBytes { get; init; }

    /// <summary>
    /// Number of bytes promoted (from low generation to high generation)
    /// </summary>
    public long PromotedBytes { get; init; }

    /// <summary>
    /// Fixed number of objects
    /// </summary>
    public long PinnedObjectsCount { get; init; }

    /// <summary>
    /// The number of objects in the queue to be finalized
    /// </summary>
    public long FinalizationPendingCount { get; init; }

    /// <summary>
    /// GC pause time
    /// </summary>
    public TimeSpan PauseDuration { get; init; }

    /// <summary>
    /// GC time proportion
    /// </summary>
    public double PauseTimePercentage { get; init; }

    /// <summary>
    /// High memory load threshold (bytes)
    /// </summary>
    public long HighMemoryLoadThresholdBytes { get; init; }

    /// <summary>
    /// Current memory load (bytes)
    /// </summary>
    public long MemoryLoadBytes { get; init; }

    /// <summary>
    /// Total available memory (bytes)
    /// </summary>
    public long TotalAvailableMemoryBytes { get; init; }

    /// <summary>
    /// Detailed information for each generation
    /// </summary>
    public GcGenerationDetails[] GenerationDetails { get; init; } = [];
}

/// <summary>
/// Details of a single generation
/// </summary>
public class GcGenerationDetails
{
    /// <summary>
    /// Algebra (0, 1, 2, 3=LOH, 4=POH)
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    /// algebra display name
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
    /// Pre-GC size (bytes)
    /// </summary>
    public long SizeBeforeBytes { get; init; }

    /// <summary>
    /// Post-GC size (bytes)
    /// </summary>
    public long SizeAfterBytes { get; init; }

    /// <summary>
    /// Pre-GC fragmentation (bytes)
    /// </summary>
    public long FragmentationBeforeBytes { get; init; }

    /// <summary>
    /// Post-GC fragmentation (bytes)
    /// </summary>
    public long FragmentationAfterBytes { get; init; }

    /// <summary>
    /// Number of bytes recycled
    /// </summary>
    public long CollectedBytes => SizeBeforeBytes - SizeAfterBytes;
}