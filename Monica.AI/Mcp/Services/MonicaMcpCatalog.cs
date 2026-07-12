using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.Core.Skills;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Modules;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Coordinates the focused MCP catalogs used by agent tools, MCP hosting, management, and connectivity.
/// </summary>
/// <remarks>
/// This type is the stable composition boundary for MCP consumers. Local server discovery, external
/// client lifetime, tool selection, and connectivity probing are implemented by separate owned services.
/// </remarks>
internal sealed class MonicaMcpCatalog : IAsyncDisposable
{
    private readonly LocalMcpServerCatalog _localCatalog;
    private readonly ExternalMcpClientPool _externalClientPool;
    private readonly McpAgentToolSource _agentToolSource;
    private readonly McpConnectivityService _connectivityService;

    /// <summary>
    /// Creates the MCP composition boundary from code-defined servers and external client registrations.
    /// </summary>
    public MonicaMcpCatalog(
        IEnumerable<MonicaMcpServer> localServers,
        IEnumerable<Skill> skills,
        IEnumerable<ExternalMcpClientProfile> codeProfiles,
        IExternalMcpClientProfileStore profileStore,
        ExternalMcpClientFactory clientFactory,
        ILoadedModuleCatalog loadedModules,
        IXmlDocumentationService xmlDocumentationService,
        IOptions<ModuleMcpOption> options,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _localCatalog = new LocalMcpServerCatalog(
            localServers,
            skills,
            loadedModules,
            xmlDocumentationService,
            options,
            serviceProvider,
            loggerFactory.CreateLogger<LocalMcpServerCatalog>());
        var externalProfileCatalog = new ExternalMcpProfileCatalog(
            codeProfiles,
            profileStore);
        _externalClientPool = new ExternalMcpClientPool(
            externalProfileCatalog,
            clientFactory,
            loggerFactory.CreateLogger<ExternalMcpClientPool>());
        _agentToolSource = new McpAgentToolSource(_localCatalog, _externalClientPool);
        _connectivityService = new McpConnectivityService(
            _localCatalog,
            externalProfileCatalog,
            clientFactory);
    }

    /// <summary>
    /// Gets whether any active local MCP server uses HTTP transport.
    /// </summary>
    public bool HasHttpServers => _localCatalog.HasHttpServers;

    /// <summary>
    /// Gets whether any active local MCP server uses stdio transport.
    /// </summary>
    public bool HasStdioServers => _localCatalog.HasStdioServers;

    /// <summary>
    /// Applies HTTP MCP server options for one logical MCP server selected by route.
    /// </summary>
    public bool TryConfigureHttpServerOptions(string? serverName, McpServerOptions serverOptions)
    {
        return _localCatalog.TryConfigureHttpServerOptions(serverName, serverOptions);
    }

    /// <summary>
    /// Applies aggregate stdio MCP server options for all startup-enabled stdio servers.
    /// </summary>
    public bool TryConfigureStdioServerOptions(McpServerOptions serverOptions, AgentCapabilityState state)
    {
        return _localCatalog.TryConfigureStdioServerOptions(serverOptions, state);
    }

    /// <summary>
    /// Gets local and external MCP tools enabled for Monica agents.
    /// </summary>
    public Task<IReadOnlyList<AITool>> GetAgentToolsAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        return _agentToolSource.GetToolsAsync(state, cancellationToken);
    }

    /// <summary>
    /// Gets management metadata for local servers and external clients.
    /// </summary>
    public async Task<IReadOnlyList<McpCatalogEntryInfo>> GetEntriesAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var entries = _localCatalog.GetEntries(state).ToList();
        var externalEntries = await _externalClientPool.GetEntriesAsync(cancellationToken);
        entries.AddRange(externalEntries.Select(entry => entry.ToInfo(state)));
        return entries;
    }

    /// <summary>
    /// Gets MCP management metadata in the shared capability shape.
    /// </summary>
    public async Task<IReadOnlyList<AgentCapabilityEntryInfo>> GetCapabilityEntriesAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        var entries = await GetEntriesAsync(state, cancellationToken);
        return entries.Select(McpCapabilityProjection.ToCapabilityInfo).ToList();
    }

    /// <summary>
    /// Tests connectivity or local readiness for one MCP catalog entry.
    /// </summary>
    public Task<McpConnectivityTestResult?> TestConnectivityAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return _connectivityService.TestAsync(name, cancellationToken);
    }

    /// <summary>
    /// Tests a draft external profile without adding it to the cached client pool.
    /// </summary>
    public Task<McpConnectivityTestResult> TestExternalProfileAsync(
        ExternalMcpClientProfile profile,
        CancellationToken cancellationToken = default)
    {
        return _connectivityService.TestExternalAsync(profile, cancellationToken);
    }

    /// <summary>
    /// Invalidates cached external tools and disposes every client created for the previous snapshot.
    /// </summary>
    public Task InvalidateExternalEntriesAsync(CancellationToken cancellationToken = default)
    {
        return _externalClientPool.InvalidateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _externalClientPool.DisposeAsync();
    }
}
