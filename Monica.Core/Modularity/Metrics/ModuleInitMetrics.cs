using System.Diagnostics.Metrics;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Exposes module initialization profiler snapshots through standard .NET observable instruments.
/// </summary>
internal sealed class ModuleInitMetrics(IMeterFactory meterFactory, MonicaApplication application)
{
    private const string STARTUP_WORK_PHASE = "startup_work";
    private const string DURATION_KIND_TAG_NAME = "kind";
    private const string EXECUTION_DURATION_KIND = "execution";
    private const string QUEUE_DURATION_KIND = "queue";
    private const string ACTIVE_SPAN_DURATION_KIND = "active_span";
    private const string BARRIER_WAIT_DURATION_KIND = "barrier_wait";
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
            ModuleInitMetricNames.StartupWorkDuration,
            () => ObserveStartupWorkDurations(application),
            unit: "s",
            description: "Monica scheduled startup-work duration by measurement kind.");

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

    private static Measurement<double>[] ObserveStartupWorkDurations(MonicaApplication application)
    {
        var composition = application.Profiling.GetCompositionPerformance();
        if (composition.StartupWorkItems.Count == 0)
        {
            return [];
        }

        return
        [
            CreateStartupWorkDurationMeasurement(
                ACTIVE_SPAN_DURATION_KIND,
                composition.ParallelWorkActiveSpanMs),
            CreateStartupWorkDurationMeasurement(EXECUTION_DURATION_KIND, composition.AggregateWorkExecutionDurationMs),
            CreateStartupWorkDurationMeasurement(QUEUE_DURATION_KIND, composition.AggregateWorkQueueDurationMs),
            CreateStartupWorkDurationMeasurement(
                BARRIER_WAIT_DURATION_KIND,
                composition.AggregateBarrierWaitDurationMs)
        ];
    }

    private static Measurement<long>[] ObserveModuleInitErrors(MonicaApplication application)
    {
        return application.Modules.RegistrationErrors
            .GroupBy(static error => error.ErrorType == ModuleRegistrationErrorType.StartupWorkError
                ? STARTUP_WORK_PHASE
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

    private static Measurement<double> CreateStartupWorkDurationMeasurement(string kind, double milliseconds)
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
