namespace Monica.AI.Mcp.Models;

/// <summary>
/// Identifies where an MCP catalog entry comes from.
/// </summary>
public enum McpCatalogSourceKind
{
    /// <summary>
    /// The entry was discovered from a Monica-defined MCP server class.
    /// </summary>
    LocalServer,

    /// <summary>
    /// The entry was registered from an external MCP client factory.
    /// </summary>
    ExternalClient
}
