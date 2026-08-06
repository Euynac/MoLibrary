namespace Monica.AI.Mcp.Models;

/// <summary>
/// Identifies where an external MCP client profile is defined.
/// </summary>
public enum ExternalMcpClientProfileOrigin
{
    /// <summary>
    /// The profile is registered by application code through the MCP module registration.
    /// </summary>
    Code,

    /// <summary>
    /// The profile is persisted by Monica's runtime management UI.
    /// </summary>
    User
}
