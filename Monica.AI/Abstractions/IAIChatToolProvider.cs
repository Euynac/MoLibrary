using Monica.AI.Tools;

namespace Monica.AI.Abstractions;

/// <summary>
/// Contributes tool-related capabilities to a chat agent during creation.
/// </summary>
public interface IAIChatToolProvider
{
    /// <summary>
    /// Configure the agent builder for the current chat context.
    /// </summary>
    Task ConfigureAsync(
        AIChatAgentBuilder builder,
        AIChatAgentCreateContext context,
        CancellationToken ct = default);
}
