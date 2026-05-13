namespace Monica.Configuration.Models;

/// <summary>
/// Detailed configuration definition DTO.
/// </summary>
public sealed record ConfigurationDefinitionDetail
{
    /// <summary>
    /// Gets the full definition.
    /// </summary>
    public required ConfigurationDefinition Definition { get; init; }
}
