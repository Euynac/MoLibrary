using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Monica.AI.Mcp.Abstractions;
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
    IEnumerable<ExternalMcpClientProfile> codeProfiles,
    IExternalMcpClientProfileStore profileStore,
    ExternalMcpClientFactory clientFactory,
    ILoadedModuleCatalog loadedModules,
    IXmlDocumentationService xmlDocumentationService,
    IOptions<ModuleMcpOption> options,
    IServiceProvider serviceProvider,
    ILogger<MonicaMcpCatalog> logger) : IAsyncDisposable
{
    private readonly IReadOnlyList<ExternalMcpClientProfile> _codeProfiles = codeProfiles
        .Select(static profile => profile.Normalize(ExternalMcpClientProfileOrigin.Code))
        .ToList();
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
    public async Task<IReadOnlyList<AITool>> GetAgentToolsAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.McpEnabled)
        {
            return [];
        }

        var localTools = _localEntries.Value
            .Where(entry => entry.IsLocalToolEnabled
                            && state.IsEntryEnabled(AgentCapabilityKind.Mcp, entry.Definition.Name))
            .SelectMany(entry => entry.Tools)
            .Select(tool => tool.AgentTool)
            .OfType<AITool>()
            .ToList();

        var externalEntries = await GetExternalEntriesAsync(cancellationToken);
        localTools.AddRange(
            externalEntries
                .Where(entry => string.IsNullOrWhiteSpace(entry.DiscoveryError)
                                && entry.Profile.IsAgentToolEnabled
                                && state.IsEntryEnabled(AgentCapabilityKind.Mcp, entry.Profile.Name))
                .SelectMany(entry => entry.Tools)
                .Cast<AITool>());

        ValidateUniqueAgentToolNames(localTools);
        return localTools;
    }

    /// <summary>
    /// Gets management metadata for local and external MCP catalog entries.
    /// </summary>
    public async Task<IReadOnlyList<McpCatalogEntryInfo>> GetEntriesAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var entries = _localEntries.Value
            .Select(entry => entry.ToInfo(state))
            .ToList();

        var externalEntries = await GetExternalEntriesAsync(cancellationToken);
        entries.AddRange(externalEntries.Select(entry => entry.ToInfo(state)));
        return entries;
    }

    /// <summary>
    /// Gets management metadata in the shared agent capability shape.
    /// </summary>
    public async Task<IReadOnlyList<AgentCapabilityEntryInfo>> GetCapabilityEntriesAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        var mcpEntries = await GetEntriesAsync(state, cancellationToken);
        return mcpEntries
            .Select(ToCapabilityInfo)
            .ToList();
    }

    /// <summary>
    /// Tests connectivity or readiness for a specific MCP catalog entry.
    /// </summary>
    public async Task<McpConnectivityTestResult?> TestConnectivityAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var localEntry = _localEntries.Value.FirstOrDefault(entry =>
            string.Equals(entry.Definition.Name, name, StringComparison.OrdinalIgnoreCase));
        if (localEntry is not null)
        {
            return await TestLocalEntryAsync(localEntry, cancellationToken);
        }

        var profile = (await GetExternalProfilesAsync(cancellationToken)).FirstOrDefault(entry =>
            string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return null;
        }

        return await TestExternalProfileAsync(profile, cancellationToken);
    }

    /// <summary>
    /// Tests a draft external MCP client profile without saving it to the runtime profile store.
    /// </summary>
    public Task<McpConnectivityTestResult> TestExternalProfileAsync(
        ExternalMcpClientProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return TestExternalProfileCoreAsync(profile.Normalize(profile.Origin), cancellationToken);
    }

    /// <summary>
    /// Clears cached external MCP tool discovery results.
    /// </summary>
    public async Task InvalidateExternalEntriesAsync(CancellationToken cancellationToken = default)
    {
        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            _externalEntries = null;
            await DisposeCreatedClientsAsync();
        }
        finally
        {
            _clientLock.Release();
        }
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
        await DisposeCreatedClientsAsync();
        _clientLock.Dispose();
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

            var profiles = await GetExternalProfilesAsync(cancellationToken);
            ValidateUniqueExternalClientNames(profiles);

            var entries = new List<ExternalMcpClientEntry>();
            foreach (var profile in profiles)
            {
                McpClient? client = null;
                try
                {
                    client = await clientFactory.CreateAsync(profile, cancellationToken);
                    var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
                    _createdClients.Add(client);
                    client = null;
                    entries.Add(new ExternalMcpClientEntry(profile, tools.ToList(), null));
                }
                catch (OperationCanceledException)
                {
                    if (client is not null)
                    {
                        await client.DisposeAsync();
                    }

                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (client is not null)
                    {
                        await client.DisposeAsync();
                    }

                    logger.LogWarning(ex, "Failed to discover tools for external MCP client profile '{Name}'.", profile.Name);
                    entries.Add(new ExternalMcpClientEntry(profile, [], ex.Message));
                }
            }

            _externalEntries = entries;
            return _externalEntries;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private async Task<McpConnectivityTestResult> TestLocalEntryAsync(
        LocalMcpServerEntry entry,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var testedAt = DateTimeOffset.UtcNow;

        if (entry.TransportKind == McpServerTransportKind.Http
            && Uri.TryCreate(entry.HttpDisplayUrl, UriKind.Absolute, out var endpoint))
        {
            try
            {
                await using var transport = new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = entry.Definition.Name,
                    Endpoint = endpoint
                });
                await using var client = await McpClient.CreateAsync(
                    transport,
                    loggerFactory: null,
                    cancellationToken: cancellationToken);
                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
                return new McpConnectivityTestResult(
                    entry.Definition.Name,
                    McpCatalogSourceKind.LocalServer,
                    true,
                    $"Connected to {endpoint}.",
                    Stopwatch.GetElapsedTime(startedAt),
                    tools.Count,
                    testedAt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new McpConnectivityTestResult(
                    entry.Definition.Name,
                    McpCatalogSourceKind.LocalServer,
                    false,
                    ex.Message,
                    Stopwatch.GetElapsedTime(startedAt),
                    0,
                    testedAt);
            }
        }

        var message = entry.TransportKind == McpServerTransportKind.Http
            ? "Local HTTP MCP server is discovered, but no absolute display URL is configured for a network probe."
            : "Local stdio MCP server is discovered. In-process readiness was checked because the running web UI cannot connect back to its own stdio transport.";

        return new McpConnectivityTestResult(
            entry.Definition.Name,
            McpCatalogSourceKind.LocalServer,
            true,
            message,
            Stopwatch.GetElapsedTime(startedAt),
            entry.Tools.Count,
            testedAt);
    }

    private async Task<McpConnectivityTestResult> TestExternalProfileCoreAsync(
        ExternalMcpClientProfile profile,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var testedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var client = await clientFactory.CreateAsync(profile, cancellationToken);
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return new McpConnectivityTestResult(
                profile.Name,
                McpCatalogSourceKind.ExternalClient,
                true,
                "Connected and listed tools.",
                Stopwatch.GetElapsedTime(startedAt),
                tools.Count,
                testedAt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new McpConnectivityTestResult(
                profile.Name,
                McpCatalogSourceKind.ExternalClient,
                false,
                ex.Message,
                Stopwatch.GetElapsedTime(startedAt),
                0,
                testedAt);
        }
    }

    private async Task<IReadOnlyList<ExternalMcpClientProfile>> GetExternalProfilesAsync(CancellationToken cancellationToken)
    {
        var userProfiles = await profileStore.LoadAsync(cancellationToken);
        return _codeProfiles
            .Concat(userProfiles)
            .OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task DisposeCreatedClientsAsync()
    {
        foreach (var client in _createdClients)
        {
            await client.DisposeAsync();
        }

        _createdClients.Clear();
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

    private static void ValidateUniqueExternalClientNames(IReadOnlyList<ExternalMcpClientProfile> profiles)
    {
        var duplicateNames = profiles
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
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

        internal McpCatalogEntryInfo ToInfo(AgentCapabilityState state)
        {
            var catalogEnabled = state.McpEnabled;
            var entryEnabled = state.IsEntryEnabled(AgentCapabilityKind.Mcp, Definition.Name);
            var disabledReason = ResolveDisabledReason(IsLocalToolEnabled, catalogEnabled, entryEnabled);
            var isAgentToolEnabled = disabledReason is null;

            return new McpCatalogEntryInfo(
                Definition.Name,
                Definition.Description,
                McpCatalogSourceKind.LocalServer,
                TransportKind,
                TransportKind == McpServerTransportKind.Http ? HttpEndpointPath : null,
                TransportKind == McpServerTransportKind.Http ? HttpDisplayUrl : null,
                null,
                false,
                null,
                IsLocalToolEnabled,
                catalogEnabled,
                entryEnabled,
                disabledReason,
                Tools.Select(tool => new McpCatalogToolInfo(
                    tool.Name,
                    tool.Description,
                    isAgentToolEnabled,
                    AgentCapabilitySchemaParser.FormatSchema(tool.SdkTool.ProtocolTool.InputSchema))).ToList());
        }
    }

    private sealed record ExternalMcpClientEntry(
        ExternalMcpClientProfile Profile,
        IReadOnlyList<McpClientTool> Tools,
        string? DiscoveryError)
    {
        internal McpCatalogEntryInfo ToInfo(AgentCapabilityState state)
        {
            var catalogEnabled = state.McpEnabled;
            var entryEnabled = state.IsEntryEnabled(AgentCapabilityKind.Mcp, Profile.Name);
            var disabledReason = ResolveDisabledReason(Profile.IsAgentToolEnabled, catalogEnabled, entryEnabled, DiscoveryError);
            var isAgentToolEnabled = disabledReason is null;

            return new McpCatalogEntryInfo(
                Profile.Name,
                Profile.Description,
                McpCatalogSourceKind.ExternalClient,
                null,
                null,
                Profile.Endpoint,
                Profile,
                Profile.IsUserManaged,
                DiscoveryError,
                Profile.IsAgentToolEnabled,
                catalogEnabled,
                entryEnabled,
                disabledReason,
                Tools.Select(tool => new McpCatalogToolInfo(
                    tool.Name,
                    tool.Description,
                    isAgentToolEnabled,
                    AgentCapabilitySchemaParser.FormatSchema(tool.JsonSchema))).ToList());
        }
    }

    private static AgentCapabilityEntryInfo ToCapabilityInfo(McpCatalogEntryInfo entry)
    {
        return new AgentCapabilityEntryInfo(
            AgentCapabilityKind.Mcp,
            entry.Name,
            entry.Name,
            entry.Description,
            null,
            entry.SourceKind.ToString(),
            [],
            entry.IsBuiltInAgentToolEnabled,
            entry.IsCatalogEnabled,
            entry.IsEntryEnabled,
            entry.DisabledReason,
            entry.Tools.Select(tool =>
            {
                var schema = TryParseSchema(tool.ParametersSchemaJson);
                return new AgentCapabilityToolInfo(
                    tool.Name,
                    tool.Description,
                    tool.IsAgentToolEnabled,
                    tool.ParametersSchemaJson,
                    AgentCapabilitySchemaParser.ParseParameters(schema));
            }).ToList(),
            [],
            entry.SourceKind,
            entry.TransportKind,
            entry.EndpointPath,
            entry.DisplayUrl,
            entry.ExternalProfile,
            entry.IsUserManaged,
            entry.DiscoveryError);
    }

    private static string? ResolveDisabledReason(
        bool builtInEnabled,
        bool catalogEnabled,
        bool entryEnabled,
        string? discoveryError = null)
    {
        if (!string.IsNullOrWhiteSpace(discoveryError))
        {
            return discoveryError;
        }

        if (!builtInEnabled)
        {
            return "This MCP entry is not configured to expose tools to Monica agents.";
        }

        if (!catalogEnabled)
        {
            return "The MCP catalog is globally disabled.";
        }

        return entryEnabled ? null : "This MCP entry is disabled in runtime capability settings.";
    }

    private static JsonElement? TryParseSchema(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(schemaJson);
        return document.RootElement.Clone();
    }
}
