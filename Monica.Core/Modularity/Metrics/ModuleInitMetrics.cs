using System.Diagnostics.Metrics;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Exposes module initialization profiler snapshots through standard .NET observable instruments.
/// </summary>
internal sealed class ModuleInitMetrics
{
    private const string MODULE_TAG_NAME = "monica.module";
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
            description: "Latest known Monica module initialization duration.");

        meter.CreateObservableGauge(
            ModuleInitMetricNames.Errors,
            ObserveModuleInitErrors,
            unit: "errors",
            description: "Current Monica module initialization errors.");
    }

    private Measurement<double>[] ObserveModuleInitDurations()
    {
        return ModuleRegistry.ModuleSnapshots
            .SelectMany(snapshot =>
            {
                var phaseDurations = ModuleInitializationProfiler
                    .GetModuleProfile(snapshot.ModuleType)
                    ?.GetPhaseDurations();

                return phaseDurations is null
                    ? []
                    : phaseDurations.Select(phaseDuration => new Measurement<double>(
                        phaseDuration.Value / 1000d,
                        new KeyValuePair<string, object?>(MODULE_TAG_NAME, snapshot.ModuleKey.ToString()),
                        new KeyValuePair<string, object?>(PHASE_TAG_NAME, FormatPhase(phaseDuration.Key))));
            })
            .ToArray();
    }

    private Measurement<long>[] ObserveModuleInitErrors()
    {
        return ModuleRegistry.ModuleRegisterErrors
            .GroupBy(error => new ModuleErrorKey(ResolveModuleName(error), FormatPhase(error.Phase)))
            .Select(group => new Measurement<long>(
                group.Count(),
                new KeyValuePair<string, object?>(MODULE_TAG_NAME, group.Key.Module),
                new KeyValuePair<string, object?>(PHASE_TAG_NAME, group.Key.Phase)))
            .ToArray();
    }

    private static string ResolveModuleName(ModuleRegistrationError error)
    {
        return ModuleRegistry.ModuleSnapshots
            .FirstOrDefault(snapshot => snapshot.ModuleType == error.ModuleType)
            ?.ModuleKey
            .ToString()
            ?? error.ModuleType.Name;
    }

    private static string FormatPhase(ModulePhase? phase)
    {
        return phase?.ToString().ToLowerInvariant() ?? "unknown";
    }

    private readonly record struct ModuleErrorKey(string Module, string Phase);
}
