using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Represents the latest scalar editor state.
/// </summary>
public sealed record ConfigurationScalarEditResult
{
    /// <summary>
    /// Gets whether the edited value passed local validation and can be staged.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the stored payload when <see cref="IsValid"/> is true.
    /// </summary>
    public ConfigurationStoredValue? StoredValue { get; init; }

    /// <summary>
    /// Gets the display-safe editor value.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets the local validation error when <see cref="IsValid"/> is false.
    /// </summary>
    public string? ValidationError { get; init; }
}
