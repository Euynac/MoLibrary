namespace Monica.AI.Models;

/// <summary>
/// AI chat request model for external API/endpoint use.
/// Note: This model is primarily for API endpoints and external integrations.
/// Internal service layer uses AgentSessionState directly.
/// </summary>
public class AIChatRequest
{
    /// <summary>
    /// Session ID (optional, creates new session if not provided)
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// User message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Provider ID (optional, uses default provider)
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Model name (optional, uses provider default model)
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// System prompt (optional)
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Whether to use streaming response
    /// </summary>
    public bool Streaming { get; set; } = true;

    /// <summary>
    /// Whether to enable reasoning/thinking mode for this request
    /// </summary>
    public bool ReasoningEnabled { get; set; }
}
