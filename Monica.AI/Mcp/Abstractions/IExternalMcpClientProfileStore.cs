using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Abstractions;

/// <summary>
/// Persists runtime-managed external MCP client profiles.
/// </summary>
public interface IExternalMcpClientProfileStore
{
    /// <summary>
    /// Loads all user-managed external MCP client profiles.
    /// </summary>
    Task<IReadOnlyList<ExternalMcpClientProfile>> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a user-managed profile. Existing profiles are matched by name.
    /// </summary>
    Task SaveAsync(ExternalMcpClientProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Removes a user-managed profile by name.
    /// </summary>
    Task<bool> DeleteAsync(string name, CancellationToken ct = default);
}
