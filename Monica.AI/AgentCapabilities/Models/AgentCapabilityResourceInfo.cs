namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Describes one readable resource exposed by an agent skill.
/// </summary>
public sealed record AgentCapabilityResourceInfo(
    string Name,
    string? Description);
