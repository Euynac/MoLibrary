using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.AgentCapabilities.Abstractions;

/// <summary>
/// Coordinates capability snapshots and persisted runtime enablement state.
/// </summary>
public interface IAgentCapabilityService
{
    /// <summary>
    /// Gets a unified management snapshot from all registered capability sources.
    /// </summary>
    Task<AgentCapabilityManagementInfo> GetManagementInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets reference-completion candidates from all registered capability sources.
    /// </summary>
    Task<IReadOnlyList<AgentCapabilityReferenceCandidate>> GetReferenceCandidatesAsync(
        string? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Changes the global runtime switch for a capability catalog and returns the refreshed snapshot.
    /// </summary>
    Task<AgentCapabilityManagementInfo> SetCatalogEnabledAsync(
        AgentCapabilityKind kind,
        bool isEnabled,
        CancellationToken ct = default);

    /// <summary>
    /// Changes the runtime switch for one capability entry and returns the refreshed snapshot.
    /// </summary>
    Task<AgentCapabilityManagementInfo> SetEntryEnabledAsync(
        AgentCapabilityKind kind,
        string name,
        bool isEnabled,
        CancellationToken ct = default);

    /// <summary>
    /// Changes whether a skill is exposed as an MCP server and returns the refreshed snapshot.
    /// </summary>
    Task<AgentCapabilityManagementInfo> SetSkillMcpServerEnabledAsync(
        string skillName,
        bool isEnabled,
        CancellationToken ct = default);

    /// <summary>
    /// Removes one persisted entry override and returns the refreshed snapshot.
    /// </summary>
    Task<AgentCapabilityManagementInfo> RemoveEntryOverrideAsync(
        AgentCapabilityKind kind,
        string name,
        CancellationToken ct = default);

    /// <summary>
    /// Increments the capability revision and returns the refreshed snapshot.
    /// </summary>
    Task<AgentCapabilityManagementInfo> RefreshAsync(CancellationToken ct = default);
}
