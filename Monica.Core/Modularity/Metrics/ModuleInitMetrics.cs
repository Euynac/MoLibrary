using System.Diagnostics.Metrics;
using Monica.Core.Modularity.Diagnostics.Models;

namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Emits terminal startup distributions and exposes only cached live counts through observable instruments.
/// </summary>
internal sealed class ModuleInitMetrics
{
    private const string KIND_TAG_NAME = "kind";
    private const string PHASE_TAG_NAME = "phase";
    private const string RESULT_TAG_NAME = "result";
    private readonly Histogram<double> _compositionDuration;
    private readonly Histogram<double> _serviceRegistrationDuration;
    private readonly Histogram<double> _typeDiscoveryDuration;
    private readonly Histogram<double> _barrierWaitDuration;
    private readonly Histogram<double> _callbackDuration;
    private readonly Histogram<double> _startupWorkDuration;
    private readonly object _recordGate = new();
    private Measurement<long>[] _liveMeasurements = [];
    private string? _recordedCompositionId;

    public ModuleInitMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ModuleInitMetricNames.MeterName);
        _compositionDuration = meter.CreateHistogram<double>(
            ModuleInitMetricNames.CompositionDuration,
            unit: "s",
            description: "Terminal Monica module-composition duration.");
        _serviceRegistrationDuration = meter.CreateHistogram<double>(
            ModuleInitMetricNames.ServiceRegistrationDuration,
            unit: "s",
            description: "Terminal Monica service-registration duration.");
        _typeDiscoveryDuration = meter.CreateHistogram<double>(
            ModuleInitMetricNames.TypeDiscoveryDuration,
            unit: "s",
            description: "Terminal aggregate Monica type-discovery duration.");
        _barrierWaitDuration = meter.CreateHistogram<double>(
            ModuleInitMetricNames.BarrierWaitDuration,
            unit: "s",
            description: "Terminal aggregate Monica startup barrier-wait duration.");
        _callbackDuration = meter.CreateHistogram<double>(
            ModuleInitMetricNames.CallbackDuration,
            unit: "s",
            description: "Terminal Monica serial module-callback duration.");
        _startupWorkDuration = meter.CreateHistogram<double>(
            ModuleInitMetricNames.StartupWorkDuration,
            unit: "s",
            description: "Terminal Monica startup-work execution and queue duration.");
        meter.CreateObservableGauge(
            ModuleInitMetricNames.LiveCount,
            ObserveLiveCounts,
            unit: "items",
            description: "Cached live Monica module-system counts by stable kind.");
    }

    /// <summary>Refreshes cached gauges and records terminal histograms exactly once per composition.</summary>
    internal void Observe(ModuleDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.IsFinal
            && string.Equals(
                Volatile.Read(ref _recordedCompositionId),
                snapshot.CompositionId,
                StringComparison.Ordinal))
        {
            return;
        }

        Volatile.Write(ref _liveMeasurements,
        [
            CreateCountMeasurement("active_modules", snapshot.Summary.ActiveModuleCount),
            CreateCountMeasurement("active_startup_work", snapshot.Summary.ActiveStartupWorkCount),
            CreateCountMeasurement("errors", snapshot.Summary.ErrorCount),
            CreateCountMeasurement("findings", snapshot.Findings.Length)
        ]);

        if (!snapshot.IsFinal)
        {
            return;
        }

        lock (_recordGate)
        {
            if (string.Equals(_recordedCompositionId, snapshot.CompositionId, StringComparison.Ordinal))
            {
                return;
            }

            var result = snapshot.Outcome?.ToString().ToLowerInvariant() ?? "unknown";
            var resultTag = new KeyValuePair<string, object?>(RESULT_TAG_NAME, result);
            _compositionDuration.Record(snapshot.Summary.TotalCompositionDurationMs / 1000d, resultTag);
            _serviceRegistrationDuration.Record(snapshot.Summary.ServiceRegistrationDurationMs / 1000d, resultTag);
            _typeDiscoveryDuration.Record(snapshot.Summary.TypeDiscoveryDurationMs / 1000d, resultTag);
            _barrierWaitDuration.Record(snapshot.Summary.AggregateBarrierWaitDurationMs / 1000d, resultTag);

            foreach (var callback in snapshot.TraceSpans.Where(static span =>
                         span.Kind == ModuleDiagnosticsTraceSpanKind.ModuleCallback))
            {
                _callbackDuration.Record(
                    callback.DurationMs / 1000d,
                    new KeyValuePair<string, object?>(KIND_TAG_NAME, Format(callback.CallbackKind)),
                    new KeyValuePair<string, object?>(PHASE_TAG_NAME, Format(callback.ModulePhase)),
                    resultTag);
            }

            foreach (var work in snapshot.TraceSpans.Where(static span =>
                         span.Kind == ModuleDiagnosticsTraceSpanKind.StartupWork))
            {
                _startupWorkDuration.Record(
                    work.DurationMs / 1000d,
                    new KeyValuePair<string, object?>(KIND_TAG_NAME, "execution"),
                    resultTag);
                _startupWorkDuration.Record(
                    (work.QueueDurationMs ?? 0) / 1000d,
                    new KeyValuePair<string, object?>(KIND_TAG_NAME, "queue"),
                    resultTag);
            }

            Volatile.Write(ref _recordedCompositionId, snapshot.CompositionId);
        }
    }

    private IEnumerable<Measurement<long>> ObserveLiveCounts() =>
        Volatile.Read(ref _liveMeasurements);

    private static Measurement<long> CreateCountMeasurement(string kind, long value)
    {
        return new Measurement<long>(
            value,
            new KeyValuePair<string, object?>(KIND_TAG_NAME, kind));
    }

    private static string Format<T>(T? value) where T : struct, Enum =>
        value?.ToString().ToLowerInvariant() ?? "unknown";
}
