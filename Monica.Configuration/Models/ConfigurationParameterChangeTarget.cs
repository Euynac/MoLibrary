namespace Monica.Configuration.Models;

/// <summary>
/// Identifies one configuration parameter whose prospective change should be analyzed.
/// </summary>
public sealed record ConfigurationParameterChangeTarget
{
    /// <summary>
    /// Gets the stable configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the exact logical path of the changed parameter.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }
}
