namespace Monica.AI.RAG.Models;

/// <summary>
/// Stores default chunker mapping for overlapping extensions.
/// </summary>
public sealed class ChunkerRoutingConfiguration
{
    /// <summary>
    /// Mapping: extension => chunker ID.
    /// </summary>
    public Dictionary<string, string> DefaultChunkers { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
