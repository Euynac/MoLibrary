namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Identifies the capability catalog that owns an agent-facing capability entry.
/// </summary>
public enum AgentCapabilityKind
{
    /// <summary>
    /// Monica skill adapted to Microsoft Agent Skills.
    /// </summary>
    Skill,

    /// <summary>
    /// MCP server or external MCP client whose tools can be exposed to Monica agents.
    /// </summary>
    Mcp
}
