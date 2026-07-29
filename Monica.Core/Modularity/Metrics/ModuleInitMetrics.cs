using System.Diagnostics.Metrics;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Exposes module initialization profiler snapshots through standard .NET observable instruments.
/// </summary>
internal sealed class ModuleInitMetrics(IMeterFactory meterFactory, MonicaApplication application)
{
    private const string COMPOSITION_WORK_PHASE = "composition_work";
    private const string DURATION_KIND_TAG_NAME = "kind";
    private const string EXECUTION_DURATION_KIND = "execution";
    private const string QUEUE_DURATION_KIND = "queue";
    private const string WALL_DURATION_KIND = "wall";
    private const string CHECKPOINT_WAIT_DURATION_KIND = "checkpoint_wait";
    private const string PHASE_TAG_NAME = "phase";
    private Meter Meter { get; } = CreateMeter(meterFactory, application);

    private static Meter CreateMeter(IMeterFactory meterFactory, MonicaApplication application)
    {
        var meter = meterFactory.Create(ModuleInitMetricNames.MeterName);

        meter.CreateObservableGauge(
            ModuleInitMetricNames.Duration,
            () => ObserveModuleInitDurations(application),
            unit: "s",
            description: "Monica module initialization duration by phase.");

        meter.CreateObservableGauge(
            ModuleInitMetricNames.CompositionWorkDuration,
            () => ObserveCompositionWorkDurations(application),
            unit: "s",
            description: "Monica scheduled composition-work duration by measurement kind.");

        meter.CreateObservableGauge(
            ModuleInitMetricNames.Errors,
            () => ObserveModuleInitErrors(application),
            unit: "errors",
            description: "Current Monica module initialization errors.");

        return meter;
    }

    private static Measurement<double>[] ObserveModuleInitDurations(MonicaApplication application)
    {
        var profiles = application.Modules.RuntimeSnapshots
            .Select(snapshot => application.Profiling.GetModuleProfile(snapshot.ModuleType))
            .OfType<ModuleProfileInfo>()
            .ToArray();
        var phases = profiles
            .SelectMany(static profile => profile.GetPhaseDurations())
            .Where(static phaseDuration => phaseDuration.Value > 0)
            .GroupBy(static phaseDuration => FormatPhase(phaseDuration.Key))
            .Select(static group => CreateDurationMeasurement(
                group.Key,
                group.Sum(static phaseDuration => phaseDuration.Value) / 1000d));
        return [.. phases];
    }

    private static Measurement<double>[] ObserveCompositionWorkDurations(MonicaApplication application)
    {
        var summary = application.Profiling.GetCompositionWorkSummary();
        if (summary.Count == 0)
        {
            return [];
        }

        return
        [
            CreateCompositionWorkDurationMeasurement(WALL_DURATION_KIND, summary.WallDurationMs),
            CreateCompositionWorkDurationMeasurement(EXECUTION_DURATION_KIND, summary.TotalExecutionDurationMs),
            CreateCompositionWorkDurationMeasurement(QUEUE_DURATION_KIND, summary.TotalQueueDurationMs),
            CreateCompositionWorkDurationMeasurement(
                CHECKPOINT_WAIT_DURATION_KIND,
                summary.TotalCheckpointWaitDurationMs)
        ];
    }

    private static Measurement<long>[] ObserveModuleInitErrors(MonicaApplication application)
    {
        return application.Modules.RegistrationErrors
            .GroupBy(static error => error.ErrorType == ModuleRegistrationErrorType.CompositionWorkError
                ? COMPOSITION_WORK_PHASE
                : FormatPhase(error.Phase))
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

    private static Measurement<double> CreateCompositionWorkDurationMeasurement(string kind, long milliseconds)
    {
        return new Measurement<double>(
            milliseconds / 1000d,
            new KeyValuePair<string, object?>(DURATION_KIND_TAG_NAME, kind));
    }

    private static string FormatPhase(ModulePhase? phase)
    {
        return phase?.ToString().ToLowerInvariant() ?? "unknown";
    }
}
