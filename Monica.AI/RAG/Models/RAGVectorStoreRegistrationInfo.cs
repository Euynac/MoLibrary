namespace Monica.AI.RAG.Models;

/// <summary>
/// Describes how the RAG vector store was registered by the module guide.
/// </summary>
public sealed record RAGVectorStoreRegistrationInfo
{
    /// <summary>
    /// Stable provider kind used by diagnostics.
    /// </summary>
    public required string ProviderKind { get; init; }

    /// <summary>
    /// User-facing provider display name.
    /// </summary>
    public required string ProviderDisplayName { get; init; }

    /// <summary>
    /// Optional registration-time configuration entries safe to show in UI.
    /// </summary>
    public IReadOnlyList<VectorStoreConfigurationEntry> ConfigurationEntries { get; init; } = [];
}
