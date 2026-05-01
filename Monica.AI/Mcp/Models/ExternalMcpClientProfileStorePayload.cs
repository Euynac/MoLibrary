namespace Monica.AI.Mcp.Models;

/// <summary>
/// File payload for UI-managed external MCP client profiles.
/// </summary>
public sealed class ExternalMcpClientProfileStorePayload
{
    /// <summary>
    /// Persisted profile schema version.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// UI-managed external MCP client profiles.
    /// </summary>
    public List<ExternalMcpClientProfile> Profiles { get; set; } = [];
}
