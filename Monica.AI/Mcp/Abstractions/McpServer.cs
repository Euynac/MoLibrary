using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Monica.AI.Mcp.Models;
using Monica.Core.Modularity.Models;

namespace Monica.AI.Mcp.Abstractions;

/// <summary>
/// Base class for Monica-defined MCP servers discovered by the MCP module.
/// </summary>
/// <remarks>
/// A server definition is inert by itself. The MCP module discovers concrete servers, converts
/// methods marked with Monica's <c>SkillToolAttribute</c> into MCP server tools, and adds optional local
/// agent tools only when the module is enabled.
/// </remarks>
public abstract class McpServer
{
    /// <summary>
    /// Initializes the runtime MCP server base. Direct inheritance is restricted; use
    /// <see cref="McpServer{TSelf}" /> for Monica MCP server authoring.
    /// </summary>
    private protected McpServer()
    {
    }

    /// <summary>
    /// Gets the framework-neutral MCP server definition.
    /// </summary>
    public abstract McpServerDefinition Definition { get; }

    /// <summary>
    /// Module keys that must be loaded for this MCP server to be available.
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Determines whether this MCP server should be included in the startup MCP catalog.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Gets the transport used when this Monica-defined server is exposed as an MCP endpoint.
    /// </summary>
    public virtual McpServerTransportKind TransportKind => McpServerTransportKind.Http;

    /// <summary>
    /// Gets whether the server's tools should also be exposed as local Monica agent tools.
    /// </summary>
    /// <remarks>
    /// Enable this for local agent testing or for intentionally reusing MCP-oriented tools inside Monica
    /// agents. HTTP or stdio MCP exposure is controlled separately by <see cref="TransportKind" />.
    /// </remarks>
    public virtual bool IsLocalToolEnabled => false;

    /// <summary>
    /// Gets serializer options used by MCP and local-agent adapters to marshal tool arguments and results.
    /// </summary>
    public virtual JsonSerializerOptions? SerializerOptions => null;
}

/// <summary>
/// Generic base class for Monica MCP servers with trim-friendly tool discovery.
/// </summary>
/// <typeparam name="TSelf">
/// Concrete MCP server type whose annotated methods should be preserved for reflection-based discovery.
/// </typeparam>
/// <remarks>
/// Prefer this base for MCP server authoring. Tool methods are Monica <c>SkillToolAttribute</c> methods;
/// no MCP SDK tool attributes are required.
/// </remarks>
public abstract class McpServer<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    TSelf> : McpServer
    where TSelf : McpServer<TSelf>
{
    /// <summary>
    /// Initializes a Monica MCP server authored with the trim-friendly generic base.
    /// </summary>
    protected McpServer()
    {
    }
}
