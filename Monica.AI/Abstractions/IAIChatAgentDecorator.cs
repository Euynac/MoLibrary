using Microsoft.Agents.AI;

namespace Monica.AI.Abstractions;

/// <summary>
/// Contributes middleware or decorators to the chat-agent pipeline.
/// </summary>
public interface IAIChatAgentDecorator
{
    /// <summary>
    /// Configure the agent pipeline for the current chat agent.
    /// </summary>
    void Configure(AIAgentBuilder builder);
}
