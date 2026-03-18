namespace Monica.AI.Tools;

/// <summary>
/// Session-scoped inputs used when building a chat agent.
/// </summary>
public sealed class AIChatAgentCreateContext
{
    /// <summary>
    /// System instructions to apply to the agent.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Knowledge bases selected for the current session.
    /// </summary>
    public IReadOnlyList<string> KnowledgeBaseIds { get; init; } = [];

    /// <summary>
    /// Whether the current session has any active knowledge bases.
    /// </summary>
    public bool HasKnowledgeBases => KnowledgeBaseIds.Count > 0;
}
