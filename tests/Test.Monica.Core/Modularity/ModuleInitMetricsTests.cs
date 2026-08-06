using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Metrics;
using Monica.Core.Modularity.Models;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleInitMetricsTests
{
    [Fact]
    public void Observe_WhenSnapshotIsLive_ShouldExposeOnlyCachedLowCardinalityGauges()
    {
        using var probe = new MetricsProbe();
        var snapshot = CreateLiveSnapshot();

        probe.Metrics.Observe(snapshot);
        probe.RecordObservableInstruments();

        var first = probe.LongMeasurements
            .Where(static measurement => measurement.InstrumentName == ModuleInitMetricNames.LiveCount)
            .ToArray();
        first.Should().HaveCount(4);
        first.Should().OnlyContain(static measurement =>
            measurement.Tags.Count == 1 && measurement.Tags.ContainsKey("kind"));
        ToCountMap(first).Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["active_modules"] = 7,
            ["active_startup_work"] = 1,
            ["errors"] = 2,
            ["findings"] = 2
        });
        probe.DoubleMeasurements.Should().BeEmpty();

        probe.LongMeasurements.Clear();
        probe.RecordObservableInstruments();

        ToCountMap(probe.LongMeasurements).Should().BeEquivalentTo(ToCountMap(first));
    }

    [Fact]
    public void Observe_WhenTerminalSnapshotIsRepeated_ShouldEmitEachHistogramOnceWithBoundedTags()
    {
        using var probe = new MetricsProbe();
        var snapshot = CreateTerminalSnapshot();

        probe.Metrics.Observe(snapshot);
        probe.Metrics.Observe(snapshot);

        probe.DoubleMeasurements.Should().HaveCount(7);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.CompositionDuration
            && Math.Abs(measurement.Value - 1.2) < 0.000_001);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.ServiceRegistrationDuration
            && Math.Abs(measurement.Value - 0.3) < 0.000_001);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.TypeDiscoveryDuration
            && Math.Abs(measurement.Value - 0.2) < 0.000_001);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.BarrierWaitDuration
            && Math.Abs(measurement.Value - 0.05) < 0.000_001);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.CallbackDuration
            && Math.Abs(measurement.Value - 0.015) < 0.000_001
            && Equals(measurement.Tags["kind"], "lifecycle")
            && Equals(measurement.Tags["phase"], "configureservices"));
        probe.DoubleMeasurements.Count(measurement =>
                measurement.InstrumentName == ModuleInitMetricNames.StartupWorkDuration)
            .Should().Be(2);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.StartupWorkDuration
            && Equals(measurement.Tags["kind"], "execution")
            && Math.Abs(measurement.Value - 0.04) < 0.000_001);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.StartupWorkDuration
            && Equals(measurement.Tags["kind"], "queue")
            && Math.Abs(measurement.Value - 0.01) < 0.000_001);

        var allowedTagNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "kind",
            "phase",
            "result"
        };
        probe.DoubleMeasurements.Should().OnlyContain(measurement =>
            measurement.Tags.Keys.All(allowedTagNames.Contains)
            && Equals(measurement.Tags["result"], "succeeded"));
        probe.DoubleMeasurements.SelectMany(static measurement => measurement.Tags.Values)
            .Should().NotContain(value =>
                Equals(value, typeof(DiagnosticsProviderModule).FullName)
                || Equals(value, "diagnostic-work-id")
                || Equals(value, "terminal-work"));
    }

    private static ModuleDiagnosticsSnapshot CreateLiveSnapshot()
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        return new ModuleDiagnosticsSnapshot
        {
            CompositionId = "live-metrics-composition",
            Revision = 3,
            StartedAtUtc = startedAtUtc,
            CapturedAtUtc = startedAtUtc,
            IsFinal = false,
            Summary = new ModuleDiagnosticsSummary
            {
                ActiveModuleCount = 7,
                ActiveStartupWorkCount = 1,
                ErrorCount = 2
            },
            TraceSpans =
            [
                new ModuleDiagnosticsTraceSpan
                {
                    SpanId = "live-work-span",
                    Kind = ModuleDiagnosticsTraceSpanKind.StartupWork,
                    WorkItemId = "live-work-id",
                    Name = "live-work",
                    StartedAtUtc = startedAtUtc,
                    StartedOffsetMs = 10,
                    EndedOffsetMs = 25
                }
            ],
            Findings =
            [
                CreateFinding("test.live.first"),
                CreateFinding("test.live.second")
            ]
        };
    }

    private static ModuleDiagnosticsSnapshot CreateTerminalSnapshot()
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var moduleKey = ModuleKey.FromModuleType(typeof(DiagnosticsProviderModule));
        return new ModuleDiagnosticsSnapshot
        {
            CompositionId = "terminal-metrics-composition",
            Revision = 9,
            StartedAtUtc = startedAtUtc,
            CapturedAtUtc = startedAtUtc.AddMilliseconds(1_200),
            IsFinal = true,
            Outcome = ModuleCompositionOutcome.Succeeded,
            Summary = new ModuleDiagnosticsSummary
            {
                ActiveModuleCount = 1,
                TotalCompositionDurationMs = 1_200,
                ServiceRegistrationDurationMs = 300,
                TypeDiscoveryDurationMs = 200,
                AggregateBarrierWaitDurationMs = 50
            },
            TraceSpans =
            [
                new ModuleDiagnosticsTraceSpan
                {
                    SpanId = "terminal-callback",
                    Kind = ModuleDiagnosticsTraceSpanKind.ModuleCallback,
                    ModuleKey = moduleKey,
                    ModulePhase = ModulePhase.ConfigureServices,
                    CallbackKind = ModuleCallbackKind.Lifecycle,
                    StartedAtUtc = startedAtUtc.AddMilliseconds(5),
                    CompletedAtUtc = startedAtUtc.AddMilliseconds(20),
                    StartedOffsetMs = 5,
                    EndedOffsetMs = 20
                },
                new ModuleDiagnosticsTraceSpan
                {
                    SpanId = "terminal-work-span",
                    Kind = ModuleDiagnosticsTraceSpanKind.StartupWork,
                    ModuleKey = moduleKey,
                    WorkItemId = "diagnostic-work-id",
                    Name = "terminal-work",
                    StartedAtUtc = startedAtUtc.AddMilliseconds(30),
                    CompletedAtUtc = startedAtUtc.AddMilliseconds(70),
                    StartedOffsetMs = 30,
                    EndedOffsetMs = 70,
                    QueueDurationMs = 10
                }
            ]
        };
    }

    private static ModuleDiagnosticFinding CreateFinding(string code) => new()
    {
        Code = code,
        Severity = ModuleDiagnosticFindingSeverity.Information,
        Evidence = new ModuleDiagnosticFindingEvidence
        {
            Kind = ModuleDiagnosticFindingEvidenceKind.PerformanceBudget
        }
    };

    private static IReadOnlyDictionary<string, long> ToCountMap(
        IEnumerable<MetricMeasurement<long>> measurements)
    {
        return measurements.ToDictionary(
            static measurement => (string)measurement.Tags["kind"]!,
            static measurement => measurement.Value,
            StringComparer.Ordinal);
    }

    private sealed class MetricsProbe : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly MeterListener _listener = new();
        private bool _capturePublications;

        internal MetricsProbe()
        {
            var services = new ServiceCollection();
            services.AddMetrics();
            _services = services.BuildServiceProvider();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (_capturePublications
                    && instrument.Meter.Name == ModuleInitMetricNames.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                LongMeasurements.Add(new MetricMeasurement<long>(
                    instrument.Name,
                    value,
                    CopyTags(tags))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                DoubleMeasurements.Add(new MetricMeasurement<double>(
                    instrument.Name,
                    value,
                    CopyTags(tags))));
            _listener.Start();
            _capturePublications = true;
            Metrics = new ModuleInitMetrics(_services.GetRequiredService<IMeterFactory>());
            _capturePublications = false;
        }

        internal ModuleInitMetrics Metrics { get; }

        internal List<MetricMeasurement<long>> LongMeasurements { get; } = [];

        internal List<MetricMeasurement<double>> DoubleMeasurements { get; } = [];

        internal void RecordObservableInstruments() => _listener.RecordObservableInstruments();

        public void Dispose()
        {
            _listener.Dispose();
            _services.Dispose();
        }

        private static IReadOnlyDictionary<string, object?> CopyTags(
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copy = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                copy[tag.Key] = tag.Value;
            }

            return copy;
        }
    }

    private sealed record MetricMeasurement<T>(
        string InstrumentName,
        T Value,
        IReadOnlyDictionary<string, object?> Tags);
}
