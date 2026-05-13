namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Represents one flat key emitted to Microsoft.Extensions.Configuration.
/// </summary>
internal sealed record ProjectedConfigurationKey
{
    /// <summary>
    /// Gets the flat configuration key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the flat configuration value.
    /// </summary>
    public string? Value { get; init; }
}
