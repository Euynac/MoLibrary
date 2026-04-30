namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Describes one JSON-schema parameter exposed by a skill script or MCP tool.
/// </summary>
public sealed record AgentCapabilityToolParameterInfo(
    string Name,
    string Type,
    string? Description,
    bool IsRequired,
    string? DefaultValue);
