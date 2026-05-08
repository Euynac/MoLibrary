namespace Monica.OpenTelemetry.InProcessCollector.Models;

/// <summary>
/// Per-process, in-memory metric snapshot collected by Monica.OpenTelemetry.
/// </summary>
public sealed class OpenTelemetrySnapshot
{
    /// <summary>
    /// Gets the service name configured for the OpenTelemetry resource.
    /// </summary>
    public required string ResourceServiceName { get; init; }

    /// <summary>
    /// Gets the optional service version configured for the OpenTelemetry resource.
    /// </summary>
    public string? ResourceServiceVersion { get; init; }

    /// <summary>
    /// Gets the optional deployment environment configured for the OpenTelemetry resource.
    /// </summary>
    public string? DeploymentEnvironment { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the snapshot was built.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Gets the meter name patterns used by the in-process collector.
    /// </summary>
    public IReadOnlyList<string> MeterPatterns { get; init; } = [];

    /// <summary>
    /// Gets the maximum retained samples per tag set.
    /// </summary>
    public int SamplesPerSeries { get; init; }

    /// <summary>
    /// Gets the maximum retained tag sets per instrument.
    /// </summary>
    public int MaxTagSetsPerInstrument { get; init; }

    /// <summary>
    /// Gets all retained instrument snapshots grouped by meter name.
    /// </summary>
    public IReadOnlyList<InstrumentSnapshot> Instruments { get; init; } = [];

    /// <summary>
    /// Gets whether any instrument hit the tag-set bound.
    /// </summary>
    public bool HasOverflow => Instruments.Any(static instrument => instrument.Overflow);
}
