using Monica.OpenTelemetry.InProcessCollector.Models;

namespace Monica.OpenTelemetry.InProcessCollector.Services.Support;

/// <summary>
/// Bounded ring buffer for one metric series identified by a stable tag set.
/// </summary>
internal sealed class MetricSeriesBuffer(
    string key,
    IReadOnlyDictionary<string, string> tags,
    int capacity,
    bool isHistogram,
    bool storesDeltaMeasurements)
{
    private readonly MetricPointSnapshot[] _samples = new MetricPointSnapshot[Math.Max(1, capacity)];
    private readonly object _gate = new();
    private int _count;
    private int _nextIndex;
    private double _runningValue;

    public string Key { get; } = key;

    public IReadOnlyDictionary<string, string> Tags { get; } = tags;

    public void Add(DateTimeOffset timestampUtc, double value)
    {
        lock (_gate)
        {
            if (storesDeltaMeasurements)
            {
                _runningValue += value;
                value = _runningValue;
            }

            _samples[_nextIndex] = new MetricPointSnapshot(timestampUtc, value);
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }
    }

    public TagSetSamples Snapshot()
    {
        lock (_gate)
        {
            var ordered = GetOrderedSamples();
            var current = ordered.Count > 0 ? ordered[^1].Value : 0;
            var delta = ordered.Count > 1 ? ordered[^1].Value - ordered[^2].Value : 0;
            var lastUpdated = ordered.Count > 0 ? ordered[^1].TimestampUtc : DateTimeOffset.MinValue;

            return new TagSetSamples
            {
                Key = Key,
                Tags = Tags,
                CurrentValue = current,
                Delta = delta,
                LastUpdatedUtc = lastUpdated,
                Samples = ordered,
                Histogram = isHistogram ? HistogramAggregator.Build(ordered) : null
            };
        }
    }

    private List<MetricPointSnapshot> GetOrderedSamples()
    {
        var ordered = new List<MetricPointSnapshot>(_count);
        var start = _count == _samples.Length ? _nextIndex : 0;

        for (var i = 0; i < _count; i++)
        {
            ordered.Add(_samples[(start + i) % _samples.Length]);
        }

        return ordered;
    }
}
