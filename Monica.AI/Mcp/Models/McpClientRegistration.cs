using ModelContextProtocol.Client;

namespace Monica.AI.Mcp.Models;

/// <summary>
/// Describes an external MCP client registered through the MCP module guide.
/// </summary>
public sealed record McpClientRegistration
{
    /// <summary>
    /// Creates an external MCP client registration.
    /// </summary>
    /// <param name="name">Stable client name shown to management UIs.</param>
    /// <param name="description">Short description of the remote MCP server or client connection.</param>
    /// <param name="clientFactory">Factory that creates and connects an MCP client when tools are first needed.</param>
    public McpClientRegistration(
        string name,
        string description,
        Func<IServiceProvider, CancellationToken, Task<McpClient>> clientFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(clientFactory);

        Name = name.Trim();
        Description = description.Trim();
        ClientFactory = clientFactory;
    }

    /// <summary>
    /// Stable client name shown to management UIs.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Short description of the remote MCP server or client connection.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Factory that creates and connects an MCP client when tools are first needed.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task<McpClient>> ClientFactory { get; }

    /// <summary>
    /// Gets whether tools listed from this client should be exposed to Monica agents.
    /// </summary>
    public bool IsAgentToolEnabled { get; init; } = true;
}
