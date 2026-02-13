namespace Monica.AI.Models;

/// <summary>
/// Represents a model available from a remote AI provider API.
/// </summary>
public class AIRemoteModelInfo
{
    /// <summary>
    /// The model identifier as returned by the provider API.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Optional metadata from the provider (e.g., display name, owner, created date).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
