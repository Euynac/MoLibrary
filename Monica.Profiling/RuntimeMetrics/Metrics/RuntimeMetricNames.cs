namespace Monica.Profiling.RuntimeMetrics.Metrics;

/// <summary>
/// Defines meter and instrument names emitted by the runtime metrics feature.
/// </summary>
public static class RuntimeMetricNames
{
    /// <summary>
    /// Meter name used for runtime metrics exported by Monica Profiling.
    /// </summary>
    public const string MeterName = "Monica.Profiling.Runtime";

    /// <summary>
    /// Observable gauge that reports managed GC heap size in bytes.
    /// </summary>
    public const string GcHeapSize = "monica.profiling.runtime.gc.heap.size";

    /// <summary>
    /// Observable gauge that reports allocation rate in bytes per second.
    /// </summary>
    public const string AllocationRate = "monica.profiling.runtime.allocation.rate";

    /// <summary>
    /// Observable gauge that reports managed heap generation sizes in bytes.
    /// </summary>
    public const string GcGenerationSize = "monica.profiling.runtime.gc.generation.size";

    /// <summary>
    /// Observable gauge that reports the percentage of time spent in GC.
    /// </summary>
    public const string TimeInGc = "monica.profiling.runtime.gc.time";

    /// <summary>
    /// Observable gauge that reports GC fragmentation percentage.
    /// </summary>
    public const string GcFragmentation = "monica.profiling.runtime.gc.fragmentation";

    /// <summary>
    /// Observable gauge that reports process working set in bytes.
    /// </summary>
    public const string WorkingSet = "monica.profiling.runtime.process.working_set";

    /// <summary>
    /// Observable gauge that reports process CPU usage percentage.
    /// </summary>
    public const string CpuUsage = "monica.profiling.runtime.process.cpu";

    /// <summary>
    /// Observable gauge that reports ThreadPool thread count.
    /// </summary>
    public const string ThreadPoolThreadCount = "monica.profiling.runtime.threadpool.thread.count";

    /// <summary>
    /// Observable counter that reports GC collections by generation.
    /// </summary>
    public const string GcCollections = "monica.profiling.runtime.gc.collections";
}
