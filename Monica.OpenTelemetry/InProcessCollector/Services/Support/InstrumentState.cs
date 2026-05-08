using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Monica.OpenTelemetry.InProcessCollector.Models;
using Monica.Modules;

namespace Monica.OpenTelemetry.InProcessCollector.Services.Support;

/// <summary>
/// Retains bounded series data for one published metric instrument.
/// </summary>
internal sealed class InstrumentState(Instrument instrument, ModuleOpenTelemetryOption option)
{
    private readonly ConcurrentDictionary<string, MetricSeriesBuffer> _series = new(StringComparer.Ordinal);
    private readonly object _seriesGate = new();
    private long _droppedSamples;

    public Instrument Instrument { get; } = instrument;

    public string Kind { get; } = InstrumentKindResolver.Resolve(instrument);

    private MetricSeriesAggregationMode AggregationMode { get; } = ResolveAggregationMode(instrument);

    public void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var normalizedTags = NormalizeTags(tags);
        var key = BuildTagSetKey(normalizedTags);

        if (!_series.TryGetValue(key, out var buffer))
        {
            lock (_seriesGate)
            {
                if (!_series.TryGetValue(key, out buffer))
                {
                    if (_series.Count >= option.MaxTagSetsPerInstrument)
                    {
                        Interlocked.Increment(ref _droppedSamples);
                        return;
                    }

                    buffer = new MetricSeriesBuffer(
                        key,
                        normalizedTags,
                        option.SamplesPerSeries,
                        AggregationMode);
                    _series[key] = buffer;
                }
            }
        }

        buffer.Add(DateTimeOffset.UtcNow, value);
    }

    private static MetricSeriesAggregationMode ResolveAggregationMode(Instrument instrument)
    {
        if (instrument.IsObservable)
        {
            return MetricSeriesAggregationMode.LatestMeasurement;
        }

        return InstrumentKindResolver.Resolve(instrument) switch
        {
            "Counter" or "UpDownCounter" => MetricSeriesAggregationMode.CumulativeDelta,
            "Histogram" => MetricSeriesAggregationMode.Histogram,
            _ => MetricSeriesAggregationMode.LatestMeasurement
        };
    }

    public InstrumentSnapshot Snapshot()
    {
        var tagSets = _series.Values
            .Select(static series => series.Snapshot())
            .OrderBy(static series => series.Key, StringComparer.Ordinal)
            .ToList();

        var droppedSamples = Interlocked.Read(ref _droppedSamples);
        return new InstrumentSnapshot
        {
            MeterName = Instrument.Meter.Name,
            Name = Instrument.Name,
            Kind = Kind,
            Unit = Instrument.Unit,
            Description = Instrument.Description,
            IsObservable = Instrument.IsObservable,
            Overflow = droppedSamples > 0,
            DroppedSamples = droppedSamples,
            TagSets = tagSets
        };
    }

    private static IReadOnlyDictionary<string, string> NormalizeTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalized = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            normalized[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        return new Dictionary<string, string>(normalized, StringComparer.Ordinal);
    }

    private static string BuildTagSetKey(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\u001f",
            tags.Select(static tag => $"{tag.Key}\u001e{tag.Value}"));
    }
}
