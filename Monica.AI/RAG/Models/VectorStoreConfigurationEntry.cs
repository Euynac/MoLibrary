namespace Monica.AI.RAG.Models;

/// <summary>
/// One display-safe vector-store configuration value used by diagnostics UI.
/// </summary>
public sealed record VectorStoreConfigurationEntry
{
    /// <summary>
    /// Configuration value label.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display-safe configuration value. Sensitive values must be redacted before assignment.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Whether the value represents a redacted sensitive setting.
    /// </summary>
    public bool IsSensitive { get; init; }
}
