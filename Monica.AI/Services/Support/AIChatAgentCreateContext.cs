namespace Monica.AI.Services.Support;

/// <summary>
/// Construction inputs used when building a chat agent.
/// </summary>
public sealed class AIChatAgentCreateContext
{
    /// <summary>
     /// System instructions to apply to the agent.
     /// </summary>
    public string? Instructions { get; init; }
}
