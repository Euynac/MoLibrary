namespace Monica.OpenTelemetry.InProcessCollector.Models;

/// <summary>
/// Retained samples for one metric instrument and one exact tag set.
/// </summary>
public sealed class TagSetSamples
{
    /// <summary>
    /// Gets the stable identifier used by the in-process collector for this tag set.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the tags associated with this series.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the latest numeric value observed for this series.
    /// </summary>
    public double CurrentValue { get; init; }

    /// <summary>
    /// Gets the delta between the two latest retained samples, or zero when fewer than two samples exist.
    /// </summary>
    public double Delta { get; init; }

    /// <summary>
    /// Gets the UTC timestamp of the latest retained sample.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; init; }

    /// <summary>
    /// Gets retained samples for this series.
    /// </summary>
    public IReadOnlyList<MetricPointSnapshot> Samples { get; init; } = [];

    /// <summary>
    /// Gets approximate histogram statistics when this series belongs to a histogram instrument.
    /// </summary>
    public HistogramSnapshot? Histogram { get; init; }
}
