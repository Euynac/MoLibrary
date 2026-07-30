namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Defines meter and instrument names emitted by the Monica module system.
/// </summary>
public static class ModuleInitMetricNames
{
    /// <summary>
    /// Meter name used for Monica module initialization metrics.
    /// </summary>
    public const string MeterName = "Monica.Core.Modularity";

    /// <summary>
    /// Observable gauge instrument that reports the latest known module initialization duration in seconds.
    /// </summary>
    public const string Duration = "monica.module.init.duration";

    /// <summary>
    /// Observable gauge instrument that reports composition-work active-span, execution, queue, and checkpoint-wait
    /// durations in seconds.
    /// </summary>
    public const string CompositionWorkDuration = "monica.module.composition.work.duration";

    /// <summary>
    /// Observable gauge instrument that reports module initialization error count by phase or composition boundary.
    /// </summary>
    public const string Errors = "monica.module.init.errors";
}
