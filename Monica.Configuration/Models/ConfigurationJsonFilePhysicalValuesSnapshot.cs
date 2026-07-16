namespace Monica.Configuration.Models;

/// <summary>
/// Captures selected physical JSON-file values and the source-content revision that contained them.
/// </summary>
public sealed record ConfigurationJsonFilePhysicalValuesSnapshot
{
    /// <summary>
    /// Gets each requested configuration path and its stored value. A null value means that the path is absent.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigurationStoredValue?> Values { get; init; } =
        new Dictionary<string, ConfigurationStoredValue?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the revision of the complete physical source content that contained <see cref="Values"/>.
    /// </summary>
    public required string Revision { get; init; }
}
