namespace Monica.OpenTelemetry.InProcessCollector.Models;

/// <summary>
/// A single numeric sample captured for one metric series.
/// </summary>
/// <param name="TimestampUtc">UTC timestamp when the sample was captured by the in-process collector.</param>
/// <param name="Value">The recorded numeric value.</param>
public sealed record MetricPointSnapshot(DateTimeOffset TimestampUtc, double Value);
