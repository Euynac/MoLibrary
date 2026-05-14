using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Represents a scalar editor value prepared for staging.
/// </summary>
public sealed record ConfigurationScalarEditResult
{
    /// <summary>
    /// Gets the stored payload.
    /// </summary>
    public required ConfigurationStoredValue StoredValue { get; init; }

    /// <summary>
    /// Gets the display-safe value.
    /// </summary>
    public string? DisplayValue { get; init; }
}
