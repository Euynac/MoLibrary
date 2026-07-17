namespace Monica.Configuration.Models;

/// <summary>
/// Captures selected values and the file revision from the same locked JSON-source read.
/// </summary>
public sealed record ConfigurationJsonFileValuesSnapshot
{
    /// <summary>
    /// Gets each requested configuration path and its stored value. A null value means that the path is absent.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigurationStoredValue?> Values { get; init; } =
        new Dictionary<string, ConfigurationStoredValue?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the source-content revision that contained <see cref="Values"/>.
    /// </summary>
    public required string Revision { get; init; }

    /// <summary>
    /// Gets an opaque revision of the current file values that are visible through the target definition schema.
    /// This revision uses Microsoft JSON-provider parsing semantics and therefore ignores formatting, comments,
    /// and properties outside the managed schema.
    /// </summary>
    public required string ProjectionRevision { get; init; }
}
