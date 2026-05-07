using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.OpenTelemetry.InProcessCollector.Models;
using Monica.OpenTelemetry.InProcessCollector.Services.Support;
using Monica.Modules;

namespace Monica.OpenTelemetry.InProcessCollector.Services;

/// <summary>
/// Bounded in-process metric collector backed by a single <see cref="MeterListener"/>.
/// </summary>
public sealed class InProcessMetricsCollector(
    IOptions<ModuleOpenTelemetryOption> options,
    ILogger<InProcessMetricsCollector> logger)
    : IHostedService, IDisposable
{
    private readonly ModuleOpenTelemetryOption _option = options.Value;
    private readonly MeterListener _listener = new();
    private readonly Dictionary<Instrument, InstrumentState> _states = [];
    private readonly object _gate = new();
    private PeriodicTimer? _observableTimer;
    private CancellationTokenSource? _observableLoopCts;
    private Task? _observableLoopTask;
    private bool _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_option.EnableInProcessCollector)
        {
            return Task.CompletedTask;
        }

        _listener.InstrumentPublished = OnInstrumentPublished;
        _listener.SetMeasurementEventCallback<byte>(RecordMeasurement);
        _listener.SetMeasurementEventCallback<short>(RecordMeasurement);
        _listener.SetMeasurementEventCallback<int>(RecordMeasurement);
        _listener.SetMeasurementEventCallback<long>(RecordMeasurement);
        _listener.SetMeasurementEventCallback<float>(RecordMeasurement);
        _listener.SetMeasurementEventCallback<double>(RecordMeasurement);
        _listener.SetMeasurementEventCallback<decimal>(RecordMeasurement);
        _listener.Start();

        _observableLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _observableTimer = new PeriodicTimer(GetObservableInterval(_option.ObservableInstrumentInterval));
        _observableLoopTask = RunObservableLoopAsync(_observableLoopCts.Token);

        logger.LogInformation(
            "Monica OpenTelemetry in-process metrics collector started for meter patterns: {MeterPatterns}",
            string.Join(", ", _option.MeterPatterns));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_observableLoopCts is not null)
        {
            await _observableLoopCts.CancelAsync();
        }

        _observableTimer?.Dispose();

        if (_observableLoopTask is not null)
        {
            try
            {
                await _observableLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _listener.Dispose();
    }

    public OpenTelemetrySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new OpenTelemetrySnapshot
            {
                ServiceName = _option.ServiceName,
                ServiceVersion = _option.ServiceVersion,
                DeploymentEnvironment = _option.DeploymentEnvironment,
                TimestampUtc = DateTimeOffset.UtcNow,
                MeterPatterns = _option.MeterPatterns.ToList(),
                SamplesPerSeries = _option.SamplesPerSeries,
                MaxTagSetsPerInstrument = _option.MaxTagSetsPerInstrument,
                Instruments = _states.Values
                    .Select(static state => state.Snapshot())
                    .OrderBy(static snapshot => snapshot.MeterName, StringComparer.Ordinal)
                    .ThenBy(static snapshot => snapshot.Name, StringComparer.Ordinal)
                    .ToList()
            };
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _observableLoopCts?.Cancel();
        _observableLoopCts?.Dispose();
        _observableTimer?.Dispose();
        _listener.Dispose();
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (!MeterPatternMatcher.IsMatch(instrument.Meter.Name, _option.MeterPatterns))
        {
            return;
        }

        InstrumentState state;
        lock (_gate)
        {
            if (!_states.TryGetValue(instrument, out state!))
            {
                state = new InstrumentState(instrument, _option);
                _states[instrument] = state;
            }
        }

        listener.EnableMeasurementEvents(instrument, state);
    }

    private static TimeSpan GetObservableInterval(TimeSpan configuredInterval)
    {
        return configuredInterval <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : configuredInterval;
    }

    private async Task RunObservableLoopAsync(CancellationToken cancellationToken)
    {
        if (_observableTimer is null)
        {
            return;
        }

        try
        {
            while (await _observableTimer.WaitForNextTickAsync(cancellationToken))
            {
                _listener.RecordObservableInstruments();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Monica OpenTelemetry in-process observable collection loop failed.");
        }
    }

    private static void RecordMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct
    {
        if (state is InstrumentState instrumentState &&
            TryConvertMeasurement(measurement, out var value))
        {
            instrumentState.Record(value, tags);
        }
    }

    private static bool TryConvertMeasurement<T>(T measurement, out double value)
        where T : struct
    {
        value = measurement switch
        {
            byte typed => typed,
            short typed => typed,
            int typed => typed,
            long typed => typed,
            float typed => typed,
            double typed => typed,
            decimal typed => (double)typed,
            _ => 0
        };

        return measurement is byte or short or int or long or float or double or decimal;
    }
}
