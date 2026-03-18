using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Tools;

namespace Monica.AI.Abstractions;

/// <summary>
/// Builds chat agents from session configuration and registered tool providers.
/// </summary>
public interface IAIChatAgentFactory
{
    /// <summary>
    /// Create a chat agent for the provided chat client and session context.
    /// </summary>
    Task<AIAgent> CreateAsync(
        IChatClient chatClient,
        AIChatAgentCreateContext context,
        CancellationToken ct = default);
}
