using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Services.Support;

namespace Monica.AI.AgentCapabilities.Services;

/// <summary>
/// Runtime-context keys used by agent capability services.
/// </summary>
public static class AgentCapabilityChatRuntimeContextKeys
{
    /// <summary>
    /// Explicit skill and MCP references selected by the user for the current chat turn.
    /// </summary>
    public static readonly AIChatRuntimeContextKey<IReadOnlyList<AgentCapabilityReference>> References =
        new("agent-capabilities.references");
}
