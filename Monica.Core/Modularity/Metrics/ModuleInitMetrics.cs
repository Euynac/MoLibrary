using System.Diagnostics.Metrics;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

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
    private const string ACTIVE_SPAN_DURATION_KIND = "active_span";
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
        var composition = application.Profiling.GetCompositionPerformance();
        var phases = composition.ModulePhaseExecutions
            .Where(static execution => execution.DurationMs > 0)
            .GroupBy(static execution => FormatPhase(execution.Phase))
            .Select(static group => CreateDurationMeasurement(
                group.Key,
                group.Sum(static execution => execution.DurationMs) / 1000d));
        return [.. phases];
    }

    private static Measurement<double>[] ObserveCompositionWorkDurations(MonicaApplication application)
    {
        var composition = application.Profiling.GetCompositionPerformance();
        if (composition.WorkItems.Count == 0)
        {
            return [];
        }

        return
        [
            CreateCompositionWorkDurationMeasurement(
                ACTIVE_SPAN_DURATION_KIND,
                composition.ParallelWorkActiveSpanMs),
            CreateCompositionWorkDurationMeasurement(EXECUTION_DURATION_KIND, composition.AggregateWorkExecutionDurationMs),
            CreateCompositionWorkDurationMeasurement(QUEUE_DURATION_KIND, composition.AggregateWorkQueueDurationMs),
            CreateCompositionWorkDurationMeasurement(
                CHECKPOINT_WAIT_DURATION_KIND,
                composition.AggregateCheckpointWaitDurationMs)
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

    private static Measurement<double> CreateCompositionWorkDurationMeasurement(string kind, double milliseconds)
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
