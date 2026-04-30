using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.AgentCapabilities.Abstractions;

/// <summary>
/// Persists global and per-entry runtime enablement for agent capabilities.
/// </summary>
public interface IAgentCapabilityStateStore
{
    /// <summary>
    /// Loads the current persisted state. Missing or empty state stores return defaults.
    /// </summary>
    Task<AgentCapabilityState> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a transformed state and increments the revision when the transformation changes state.
    /// </summary>
    Task<AgentCapabilityState> UpdateAsync(
        Func<AgentCapabilityState, bool> update,
        CancellationToken ct = default);
}
