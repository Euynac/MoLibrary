namespace Monica.Core.Modularity.Metrics;

/// <summary>Defines meter and instrument names emitted by the Monica module system.</summary>
public static class ModuleInitMetricNames
{
    /// <summary>Gets the meter name hosts subscribe to.</summary>
    public const string MeterName = "Monica.Core.Modularity";

    /// <summary>Histogram for terminal end-to-end composition duration.</summary>
    public const string CompositionDuration = "monica.module.composition.duration";

    /// <summary>Histogram for explicitly tracked application startup through <c>ApplicationStarted</c>.</summary>
    public const string ApplicationStartupDuration = "monica.application.startup.duration";

    /// <summary>Histogram for terminal service-registration duration.</summary>
    public const string ServiceRegistrationDuration = "monica.module.service_registration.duration";

    /// <summary>Histogram for terminal aggregate type-discovery duration.</summary>
    public const string TypeDiscoveryDuration = "monica.module.type_discovery.duration";

    /// <summary>Histogram for terminal aggregate startup barrier-wait duration.</summary>
    public const string BarrierWaitDuration = "monica.module.barrier_wait.duration";

    /// <summary>Histogram for terminal serial module callback durations.</summary>
    public const string CallbackDuration = "monica.module.callback.duration";

    /// <summary>Histogram for terminal startup-work execution durations.</summary>
    public const string StartupWorkDuration = "monica.module.startup_work.duration";

    /// <summary>Observable gauge for live bounded module-system counts.</summary>
    public const string LiveCount = "monica.module.live.count";
}
