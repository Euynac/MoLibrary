namespace Monica.OpenTelemetry.InProcessCollector.Models;

/// <summary>
/// Snapshot of one published .NET metric instrument and its retained tag-set series.
/// </summary>
public sealed class InstrumentSnapshot
{
    /// <summary>
    /// Gets the meter name that owns the instrument.
    /// </summary>
    public required string MeterName { get; init; }

    /// <summary>
    /// Gets the instrument name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the best-effort instrument kind inferred from the instrument type.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the unit declared by the instrument.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Gets the instrument description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether the instrument is observable.
    /// </summary>
    public bool IsObservable { get; init; }

    /// <summary>
    /// Gets whether the collector dropped additional tag sets after reaching the configured bound.
    /// </summary>
    public bool Overflow { get; init; }

    /// <summary>
    /// Gets the number of dropped samples caused by tag-set overflow.
    /// </summary>
    public long DroppedSamples { get; init; }

    /// <summary>
    /// Gets retained tag-set series for this instrument.
    /// </summary>
    public IReadOnlyList<TagSetSamples> TagSets { get; init; } = [];

    /// <summary>
    /// Gets the latest numeric value across all retained tag sets.
    /// </summary>
    public double CurrentValue => TagSets.Sum(static tagSet => tagSet.CurrentValue);

    /// <summary>
    /// Gets the latest delta across all retained tag sets.
    /// </summary>
    public double Delta => TagSets.Sum(static tagSet => tagSet.Delta);
}
