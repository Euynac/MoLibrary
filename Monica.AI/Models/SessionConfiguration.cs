namespace Monica.AI.Models;

/// <summary>
/// Unified configuration for AI chat session creation and updates.
/// Contains all mutable settings that can trigger agent recreation.
/// </summary>
public class SessionConfiguration
{
    /// <summary>
    /// Provider ID (optional, uses default if not specified)
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Model name (optional, uses provider default if not specified)
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// System prompt/instructions (optional)
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Knowledge base IDs for RAG integration (optional, null disables RAG)
    /// </summary>
    public List<string>? KnowledgeBaseIds { get; set; }

    /// <summary>
    /// Whether to enable reasoning/thinking mode
    /// </summary>
    public bool ReasoningEnabled { get; set; }

    /// <summary>
    /// Session title (optional)
    /// </summary>
    public string? Title { get; set; }
}
