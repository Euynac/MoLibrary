using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.AgentCapabilities.Abstractions;

/// <summary>
/// Contributes one independently registered capability catalog to agent management.
/// </summary>
/// <remarks>
/// Implementations must return a self-consistent snapshot for the supplied persisted state. Sources
/// are discovered through dependency injection, allowing Skills, MCP, and future capability modules
/// to remain optional additions to the core AI module.
/// </remarks>
public interface IAgentCapabilitySource
{
    /// <summary>
    /// Gets the capability catalog owned by this source.
    /// </summary>
    AgentCapabilityKind Kind { get; }

    /// <summary>
    /// Builds management metadata after applying the supplied runtime enablement state.
    /// </summary>
    /// <param name="state">Persisted capability state for the current host.</param>
    /// <param name="ct">Cancellation token for asynchronous source discovery.</param>
    Task<AgentCapabilitySourceSnapshot> GetSnapshotAsync(
        AgentCapabilityState state,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates runtime resources derived from capability state after a persisted state change.
    /// </summary>
    /// <remarks>
    /// Stateless sources can use the default no-op implementation. Sources that cache live clients,
    /// tools, or other state-dependent resources must release them before returning.
    /// </remarks>
    Task InvalidateAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
