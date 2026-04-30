namespace Monica.AI.Mcp.Models;

/// <summary>
/// Describes one tool available from an MCP catalog entry.
/// </summary>
public sealed record McpCatalogToolInfo
{
    /// <summary>
    /// Creates MCP tool metadata.
    /// </summary>
    public McpCatalogToolInfo(
        string name,
        string? description,
        bool isAgentToolEnabled,
        string? parametersSchemaJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description;
        IsAgentToolEnabled = isAgentToolEnabled;
        ParametersSchemaJson = parametersSchemaJson;
    }

    /// <summary>
    /// Tool name advertised by the MCP entry.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional tool description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Whether this tool is currently exposed directly to Monica agents.
    /// </summary>
    public bool IsAgentToolEnabled { get; }

    /// <summary>
    /// Raw JSON schema for input parameters, formatted for inspection.
    /// </summary>
    public string? ParametersSchemaJson { get; }
}
