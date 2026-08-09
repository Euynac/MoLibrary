using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Metrics;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

[Collection(BlockingConcurrencyCollection.Name)]
public sealed class ModuleInitMetricsTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ApplicationStartup_WhenTrackedHostStopsImmediately_ShouldStillPublishTheTerminalDuration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(MonicaStartup.Start(), monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
        });
        using var host = builder.Build();
        var measurements = new ConcurrentQueue<double>();
        using var listener = ListenForApplicationStartup(host, measurements);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        measurements.Should().ContainSingle().Which.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplicationStartup_WhenMetricsWorkerIsCancelledBeforeStarting_ShouldPublishDuringStop()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(MonicaStartup.Start(), monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
        });
        using var host = builder.Build();
        var measurements = new ConcurrentQueue<double>();
        using var listener = ListenForApplicationStartup(host, measurements);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var activationService = host.Services.GetServices<IHostedService>()
            .OfType<ModuleInitMetricsActivationService>()
            .Single();
        application.Profiling.MarkApplicationReady();

        await activationService.StartAsync(new CancellationToken(canceled: true));
        measurements.Should().BeEmpty();
        await activationService.StopAsync(TestContext.Current.CancellationToken);

        measurements.Should().ContainSingle().Which.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplicationStartup_WhenNoBarrierWorkIsStillRunning_ShouldPublishBeforeModuleFinality()
    {
        using var gate = new ControlledWorkGate();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(MonicaStartup.Start(), monica =>
        {
            monica.ConfigureModuleSystem(static options => options.MaxConcurrentStartupWorkItems = 2);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            monica.AddModule<StartupWorkProbeModuleOne, StartupWorkProbeModuleOneOption>(options =>
                options.AddConfigureServicesWork(
                    "live-application-metric-work",
                    gate.Run,
                    ModuleStartupWorkBarrier.NoBarrier));
        });
        using var host = builder.Build();
        var measurements = new ConcurrentQueue<double>();
        using var listener = ListenForApplicationStartup(host, measurements);

        try
        {
            gate.WaitUntilEntered(HANG_GUARD, TestContext.Current.CancellationToken);
            await host.StartAsync(TestContext.Current.CancellationToken);
            var snapshot = host.Services.GetRequiredService<ModuleDiagnosticsFacade>().GetSnapshot().Data!;

            snapshot.IsFinal.Should().BeFalse();
            snapshot.Summary.ApplicationStartupDurationMs.Should().NotBeNull();
            await WaitUntilAsync(() => measurements.Count == 1);
            measurements.Should().ContainSingle().Which.Should().BeGreaterThan(0);
        }
        finally
        {
            gate.Release();
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        measurements.Should().ContainSingle();
    }

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
    public void Observe_WhenApplicationStartupCompletesBeforeModuleFinality_ShouldTagReadinessAsSucceeded()
    {
        using var probe = new MetricsProbe();
        var liveSnapshot = CreateLiveSnapshot();
        liveSnapshot = liveSnapshot with
        {
            Summary = liveSnapshot.Summary with { ApplicationStartupDurationMs = 250 }
        };

        probe.Metrics.Observe(liveSnapshot);

        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.ApplicationStartupDuration
            && Math.Abs(measurement.Value - 0.25) < 0.000_001
            && Equals(measurement.Tags["result"], "succeeded"));
        probe.DoubleMeasurements.Should().NotContain(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.CompositionDuration);
    }

    [Fact]
    public void Observe_WhenTerminalSnapshotIsRepeated_ShouldEmitEachHistogramOnceWithBoundedTags()
    {
        using var probe = new MetricsProbe();
        var snapshot = CreateTerminalSnapshot();

        probe.Metrics.Observe(snapshot);
        probe.Metrics.Observe(snapshot);

        probe.DoubleMeasurements.Should().HaveCount(8);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.CompositionDuration
            && Math.Abs(measurement.Value - 1.2) < 0.000_001);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.ApplicationStartupDuration
            && Math.Abs(measurement.Value - 1.5) < 0.000_001);
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

    [Fact]
    public void Observe_WhenApplicationStartupArrivesAfterFinalComposition_ShouldUseIndependentLatches()
    {
        using var probe = new MetricsProbe();
        var ready = CreateTerminalSnapshot();
        var beforeReady = ready with
        {
            Revision = ready.Revision - 1,
            Summary = ready.Summary with { ApplicationStartupDurationMs = null }
        };

        probe.Metrics.Observe(beforeReady);

        probe.DoubleMeasurements.Should().HaveCount(7);
        probe.DoubleMeasurements.Should().NotContain(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.ApplicationStartupDuration);

        probe.Metrics.Observe(ready);

        probe.DoubleMeasurements.Should().HaveCount(8);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.ApplicationStartupDuration);
        probe.DoubleMeasurements.Count(measurement =>
                measurement.InstrumentName == ModuleInitMetricNames.CompositionDuration)
            .Should().Be(1);

        probe.Metrics.Observe(ready);
        probe.DoubleMeasurements.Should().HaveCount(8);
    }

    [Fact]
    public void MetricsProbe_WhenCompetingFactoryPublishesDuringSetup_ShouldIgnoreForeignMeasurements()
    {
        using var competingServices = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();
        var snapshot = CreateTerminalSnapshot();
        using var probe = new MetricsProbe(() =>
        {
            var competingMetrics = new ModuleInitMetrics(
                competingServices.GetRequiredService<IMeterFactory>());
            competingMetrics.Observe(snapshot);
        });

        probe.DoubleMeasurements.Should().BeEmpty();

        probe.Metrics.Observe(snapshot);

        probe.DoubleMeasurements.Should().HaveCount(8);
        probe.DoubleMeasurements.Should().ContainSingle(measurement =>
            measurement.InstrumentName == ModuleInitMetricNames.CompositionDuration);
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
                ApplicationStartupDurationMs = 1_500,
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

    private static MeterListener ListenForApplicationStartup(
        IHost host,
        ConcurrentQueue<double> measurements)
    {
        var meterFactory = host.Services.GetRequiredService<IMeterFactory>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, meterFactory)
                && instrument.Name == ModuleInitMetricNames.ApplicationStartupDuration)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => measurements.Enqueue(value));
        listener.Start();
        return listener;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < timeout)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        condition().Should().BeTrue();
    }

    private sealed class ControlledWorkGate : IDisposable
    {
        private readonly ManualResetEventSlim _entered = new(initialState: false);
        private readonly ManualResetEventSlim _release = new(initialState: false);

        internal void WaitUntilEntered(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!_entered.Wait(timeout, cancellationToken))
            {
                throw new TimeoutException("The controlled metrics work did not start.");
            }
        }

        internal void Run()
        {
            _entered.Set();
            _release.Wait();
        }

        internal void Release() => _release.Set();

        public void Dispose()
        {
            _release.Set();
            _entered.Dispose();
            _release.Dispose();
        }
    }

    private sealed class MetricsProbe : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly MeterListener _listener = new();

        internal MetricsProbe(Action? publishCompetingMetrics = null)
        {
            var services = new ServiceCollection();
            services.AddMetrics();
            _services = services.BuildServiceProvider();
            var meterFactory = _services.GetRequiredService<IMeterFactory>();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter.Scope, meterFactory)
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
            publishCompetingMetrics?.Invoke();
            Metrics = new ModuleInitMetrics(meterFactory);
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
