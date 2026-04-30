namespace Monica.AI.Mcp.Models;

/// <summary>
/// Describes a Monica-defined MCP server independently of the concrete MCP SDK hosting layer.
/// </summary>
public sealed record McpServerDefinition
{
    /// <summary>
    /// Creates an MCP server definition.
    /// </summary>
    /// <param name="name">Stable MCP server identifier shown to MCP clients and management UIs.</param>
    /// <param name="description">Short description of the server's capabilities.</param>
    public McpServerDefinition(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Name = name.Trim();
        Description = description.Trim();
    }

    /// <summary>
    /// Stable MCP server identifier shown to MCP clients and management UIs.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Short description of the server's capabilities.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Optional implementation version reported to MCP clients.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Optional display title shown by MCP clients and management UIs.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional instructions sent to MCP clients during initialization.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Optional website URL for documentation or management UI links.
    /// </summary>
    public string? WebsiteUrl { get; init; }
}
