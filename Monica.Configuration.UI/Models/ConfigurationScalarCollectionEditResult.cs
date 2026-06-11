using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Represents the latest scalar collection editor state.
/// </summary>
public sealed record ConfigurationScalarCollectionEditResult
{
    /// <summary>
    /// Gets whether the edited collection passed local validation and can be staged.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the stored collection payload when <see cref="IsValid"/> is true.
    /// </summary>
    public ConfigurationStoredValue? StoredValue { get; init; }

    /// <summary>
    /// Gets the display-safe collection value.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets the display-safe invalid item or row value when <see cref="IsValid"/> is false.
    /// </summary>
    public string? InvalidDisplayValue { get; init; }

    /// <summary>
    /// Gets the local validation error when <see cref="IsValid"/> is false.
    /// </summary>
    public string? ValidationError { get; init; }
}
