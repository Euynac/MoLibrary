using System.Diagnostics.Metrics;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Exposes module initialization profiler snapshots through standard .NET observable instruments.
/// </summary>
internal sealed class ModuleInitMetrics
{
    private const string PHASE_TAG_NAME = "phase";

    /// <summary>
    /// Initializes module initialization observable instruments.
    /// </summary>
    public ModuleInitMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ModuleInitMetricNames.MeterName);

        meter.CreateObservableGauge(
            ModuleInitMetricNames.Duration,
            ObserveModuleInitDurations,
            unit: "s",
            description: "Monica module initialization duration by phase.");

        meter.CreateObservableGauge(
            ModuleInitMetricNames.Errors,
            ObserveModuleInitErrors,
            unit: "errors",
            description: "Current Monica module initialization errors.");
    }

    private static Measurement<double>[] ObserveModuleInitDurations()
    {
        return ModuleRegistry.ModuleSnapshots
            .Select(snapshot => ModuleInitializationProfiler.GetModuleProfile(snapshot.ModuleType))
            .OfType<ModuleProfileInfo>()
            .SelectMany(static profile => profile.GetPhaseDurations())
            .Where(static phaseDuration => phaseDuration.Value > 0)
            .GroupBy(static phaseDuration => FormatPhase(phaseDuration.Key))
            .Select(static group => CreateDurationMeasurement(
                group.Key,
                group.Sum(static phaseDuration => phaseDuration.Value) / 1000d))
            .ToArray();
    }

    private static Measurement<long>[] ObserveModuleInitErrors()
    {
        return ModuleRegistry.ModuleRegisterErrors
            .GroupBy(static error => FormatPhase(error.Phase))
            .Select(group => new Measurement<long>(
                group.Count(),
                new KeyValuePair<string, object?>(PHASE_TAG_NAME, group.Key)))
            .ToArray();
    }

    private static Measurement<double> CreateDurationMeasurement(string phase, double value)
    {
        return new Measurement<double>(
            value,
            new KeyValuePair<string, object?>(PHASE_TAG_NAME, phase));
    }

    private static string FormatPhase(ModulePhase? phase)
    {
        return phase?.ToString().ToLowerInvariant() ?? "unknown";
    }
}
