namespace Monica.Core.Modularity.Models;

/// <summary>
/// Marks endpoints owned by Monica so shared routing policies can be applied without assembly inference.
/// </summary>
/// <param name="kind">The Monica endpoint category.</param>
public sealed class MonicaEndpointMetadata(MonicaEndpointKind kind)
{
    /// <summary>
    /// Gets the Monica endpoint category.
    /// </summary>
    public MonicaEndpointKind Kind { get; } = kind;
}
