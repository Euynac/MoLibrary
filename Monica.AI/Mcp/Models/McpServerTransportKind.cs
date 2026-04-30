namespace Monica.AI.Mcp.Models;

/// <summary>
/// MCP transports supported by Monica-defined MCP servers.
/// </summary>
public enum McpServerTransportKind
{
    /// <summary>
    /// Exposes the server through ASP.NET Core Streamable HTTP.
    /// </summary>
    Http,

    /// <summary>
    /// Exposes the server through standard input and output for local MCP clients.
    /// </summary>
    Stdio
}
