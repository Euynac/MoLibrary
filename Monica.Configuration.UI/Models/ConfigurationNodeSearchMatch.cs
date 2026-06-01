using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Represents a configuration node match shown in the definition search list.
/// </summary>
public sealed record ConfigurationNodeSearchMatch
{
    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the matched configuration node.
    /// </summary>
    public required ConfigurationNodeDefinition Node { get; init; }

    /// <summary>
    /// Gets the display label for the matched node.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the canonical logical path label.
    /// </summary>
    public required string PathLabel { get; init; }

    /// <summary>
    /// Gets the optional node description.
    /// </summary>
    public string? Description { get; init; }
}
