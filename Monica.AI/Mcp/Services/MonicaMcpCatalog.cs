using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.Core.Modularity.Models;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Modules;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Maintains the active MCP server and client catalog used by Monica agents and MCP hosting.
/// </summary>
public sealed class MonicaMcpCatalog(
    IEnumerable<MonicaMcpServer> localServers,
    IEnumerable<McpClientRegistration> clientRegistrations,
    ILoadedModuleCatalog loadedModules,
    IXmlDocumentationService xmlDocumentationService,
    IOptions<ModuleMcpOption> options,
    IServiceProvider serviceProvider,
    ILogger<MonicaMcpCatalog> logger) : IAsyncDisposable
{
    private readonly IReadOnlyList<McpClientRegistration> _clientRegistrations = clientRegistrations.ToList();
    private readonly List<McpClient> _createdClients = [];
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly Lazy<IReadOnlyList<LocalMcpServerEntry>> _localEntries = new(() =>
        BuildLocalEntries(
            localServers.ToList(),
            loadedModules.GetLoadedModuleKeys(),
            options.Value,
            serviceProvider,
            xmlDocumentationService,
            logger));
    private IReadOnlyList<ExternalMcpClientEntry>? _externalEntries;
    private bool _disposed;

    /// <summary>
    /// Gets whether any active local MCP server uses HTTP transport.
    /// </summary>
    public bool HasHttpServers => _localEntries.Value.Any(entry => entry.TransportKind == McpServerTransportKind.Http);

    /// <summary>
    /// Gets whether any active local MCP server uses stdio transport.
    /// </summary>
    public bool HasStdioServers => _localEntries.Value.Any(entry => entry.TransportKind == McpServerTransportKind.Stdio);

    /// <summary>
    /// Gets active MCP server tools exposed through Monica's MCP endpoint.
    /// </summary>
    public IReadOnlyList<McpServerTool> GetMcpServerTools()
    {
        return _localEntries.Value
            .SelectMany(entry => entry.Tools)
            .Select(tool => tool.SdkTool)
            .ToList();
    }

    /// <summary>
    /// Gets tools that should be exposed directly to Monica agents.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetAgentToolsAsync(CancellationToken cancellationToken = default)
    {
        var localTools = _localEntries.Value
            .Where(entry => entry.IsLocalToolEnabled)
            .SelectMany(entry => entry.Tools)
            .Select(tool => tool.AgentTool)
            .OfType<AITool>()
            .ToList();

        var externalEntries = await GetExternalEntriesAsync(cancellationToken);
        localTools.AddRange(
            externalEntries
                .Where(entry => entry.Registration.IsAgentToolEnabled)
                .SelectMany(entry => entry.Tools)
                .Cast<AITool>());

        ValidateUniqueAgentToolNames(localTools);
        return localTools;
    }

    /// <summary>
    /// Gets management metadata for local and external MCP catalog entries.
    /// </summary>
    public async Task<IReadOnlyList<McpCatalogEntryInfo>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        var entries = _localEntries.Value
            .Select(static entry => entry.ToInfo())
            .ToList();

        var externalEntries = await GetExternalEntriesAsync(cancellationToken);
        entries.AddRange(externalEntries.Select(static entry => entry.ToInfo()));
        return entries;
    }

    /// <summary>
    /// Creates MCP server implementation metadata from the active local server catalog.
    /// </summary>
    public Implementation CreateServerImplementation()
    {
        var localEntries = _localEntries.Value;
        var title = localEntries.Count == 1
            ? localEntries[0].Definition.Title
            : "Monica MCP";

        return new Implementation
        {
            Name = localEntries.Count == 1 ? localEntries[0].Definition.Name : "monica-mcp",
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            Version = localEntries.Count == 1 ? localEntries[0].Definition.Version : "1.0.0",
            Description = localEntries.Count == 1
                ? localEntries[0].Definition.Description
                : "Aggregated MCP tools exposed by Monica.",
            WebsiteUrl = localEntries.Count == 1 ? localEntries[0].Definition.WebsiteUrl : null
        };
    }

    /// <summary>
    /// Creates initialization instructions from all active local MCP servers.
    /// </summary>
    public string? CreateServerInstructions()
    {
        var instructions = _localEntries.Value
            .Select(entry => entry.Definition.Instructions)
            .Where(instruction => !string.IsNullOrWhiteSpace(instruction))
            .ToList();

        return instructions.Count == 0
            ? null
            : string.Join(Environment.NewLine + Environment.NewLine, instructions);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clientLock.Dispose();

        foreach (var client in _createdClients)
        {
            await client.DisposeAsync();
        }
    }

    private async Task<IReadOnlyList<ExternalMcpClientEntry>> GetExternalEntriesAsync(CancellationToken cancellationToken)
    {
        if (_externalEntries is not null)
        {
            return _externalEntries;
        }

        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            if (_externalEntries is not null)
            {
                return _externalEntries;
            }

            var entries = new List<ExternalMcpClientEntry>();
            foreach (var registration in _clientRegistrations)
            {
                var client = await registration.ClientFactory(serviceProvider, cancellationToken);
                _createdClients.Add(client);

                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
                entries.Add(new ExternalMcpClientEntry(registration, tools.ToList()));
            }

            ValidateUniqueExternalClientNames(entries);
            _externalEntries = entries;
            return _externalEntries;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private static IReadOnlyList<LocalMcpServerEntry> BuildLocalEntries(
        IReadOnlyList<MonicaMcpServer> servers,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ModuleMcpOption options,
        IServiceProvider serviceProvider,
        IXmlDocumentationService xmlDocumentationService,
        ILogger logger)
    {
        var activeServers = servers
            .Where(server => IsActive(server, loadedModuleKeys, logger))
            .OrderBy(server => server.Definition.Name, StringComparer.Ordinal)
            .ToList();

        ValidateUniqueLocalServerNames(activeServers);

        var entries = activeServers
            .Select(server =>
            {
                var tools = McpServerToolDiscovery.Discover(server, serviceProvider, xmlDocumentationService);
                return new LocalMcpServerEntry(server, options.McpHttpEndpointPath, options.McpHttpDisplayUrl, tools);
            })
            .ToList();

        ValidateUniqueMcpServerToolNames(entries.SelectMany(entry => entry.Tools));
        return entries;
    }

    private static bool IsActive(
        MonicaMcpServer server,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ILogger logger)
    {
        if (!server.IsEnabled)
        {
            logger.LogDebug("Skipping disabled MCP server '{McpServerName}'.", server.Definition.Name);
            return false;
        }

        var missing = server.RequiredModules
            .Where(required => !loadedModuleKeys.Contains(required))
            .ToList();

        if (missing.Count == 0)
        {
            return true;
        }

        logger.LogDebug(
            "Skipping MCP server '{McpServerName}' because required modules are not loaded: {RequiredModules}.",
            server.Definition.Name,
            string.Join(", ", missing));
        return false;
    }

    private static void ValidateUniqueLocalServerNames(IReadOnlyList<MonicaMcpServer> servers)
    {
        var duplicateNames = servers
            .GroupBy(server => server.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate MCP server names are not allowed: " +
                string.Join(", ", duplicateNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) + ".");
        }
    }

    private static void ValidateUniqueExternalClientNames(IReadOnlyList<ExternalMcpClientEntry> entries)
    {
        var duplicateNames = entries
            .GroupBy(entry => entry.Registration.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate MCP client names are not allowed: " +
                string.Join(", ", duplicateNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) + ".");
        }
    }

    private static void ValidateUniqueMcpServerToolNames(IEnumerable<McpServerToolDescriptor> tools)
    {
        var duplicateNames = tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate MCP server tool names are not allowed: " +
                string.Join(", ", duplicateNames.OrderBy(name => name, StringComparer.Ordinal)) + ".");
        }
    }

    private static void ValidateUniqueAgentToolNames(IEnumerable<AITool> tools)
    {
        var duplicateNames = tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate Monica agent tool names are not allowed: " +
                string.Join(", ", duplicateNames.OrderBy(name => name, StringComparer.Ordinal)) + ".");
        }
    }

    private sealed record LocalMcpServerEntry(
        MonicaMcpServer Server,
        string HttpEndpointPath,
        string? HttpDisplayUrl,
        IReadOnlyList<McpServerToolDescriptor> Tools)
    {
        internal McpServerDefinition Definition => Server.Definition;

        internal McpServerTransportKind TransportKind => Server.TransportKind;

        internal bool IsLocalToolEnabled => Server.IsLocalToolEnabled;

        internal McpCatalogEntryInfo ToInfo()
        {
            return new McpCatalogEntryInfo(
                Definition.Name,
                Definition.Description,
                McpCatalogSourceKind.LocalServer,
                TransportKind,
                TransportKind == McpServerTransportKind.Http ? HttpEndpointPath : null,
                TransportKind == McpServerTransportKind.Http ? HttpDisplayUrl : null,
                IsLocalToolEnabled,
                Tools.Select(tool => new McpCatalogToolInfo(tool.Name, tool.Description, IsLocalToolEnabled)).ToList());
        }
    }

    private sealed record ExternalMcpClientEntry(
        McpClientRegistration Registration,
        IReadOnlyList<McpClientTool> Tools)
    {
        internal McpCatalogEntryInfo ToInfo()
        {
            return new McpCatalogEntryInfo(
                Registration.Name,
                Registration.Description,
                McpCatalogSourceKind.ExternalClient,
                null,
                null,
                null,
                Registration.IsAgentToolEnabled,
                Tools.Select(tool => new McpCatalogToolInfo(tool.Name, tool.Description, Registration.IsAgentToolEnabled)).ToList());
        }
    }
}
