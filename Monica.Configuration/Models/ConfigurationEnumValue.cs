namespace Monica.Configuration.Models;

/// <summary>
/// Describes one portable enum member for configuration editing and validation.
/// </summary>
public sealed record ConfigurationEnumValue
{
    /// <summary>
    /// Gets the enum member name used by JSON editing, option binding, and display controls.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the invariant-culture numeric value of the enum member.
    /// </summary>
    public required string Value { get; init; }
}
