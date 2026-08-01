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
    /// Observable gauge instrument that reports startup-work active-span, execution, queue, and barrier-wait
    /// durations in seconds.
    /// </summary>
    public const string StartupWorkDuration = "monica.module.startup.work.duration";

    /// <summary>
    /// Observable gauge instrument that reports module initialization error count by phase or startup boundary.
    /// </summary>
    public const string Errors = "monica.module.init.errors";
}
