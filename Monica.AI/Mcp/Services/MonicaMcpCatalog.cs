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
using Monica.Core.Skills;
using Monica.Core.Skills.Models;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Modules;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Maintains the active MCP server and client catalog used by Monica agents and MCP hosting.
/// </summary>
public sealed class MonicaMcpCatalog(
    IEnumerable<MonicaMcpServer> localServers,
    IEnumerable<Skill> skills,
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
            skills.ToList(),
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
    /// Applies HTTP MCP server options for one logical MCP server selected by route.
    /// </summary>
    public bool TryConfigureHttpServerOptions(string? serverName, McpServerOptions serverOptions)
    {
        ArgumentNullException.ThrowIfNull(serverOptions);
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return false;
        }

        var entry = _localEntries.Value.FirstOrDefault(entry =>
            entry.TransportKind == McpServerTransportKind.Http
            && string.Equals(entry.Definition.Name, serverName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        ConfigureServerOptions(serverOptions, entry);
        return true;
    }

    /// <summary>
    /// Applies aggregate stdio MCP server options for all active stdio MCP servers.
    /// </summary>
    public bool TryConfigureStdioServerOptions(McpServerOptions serverOptions, AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(serverOptions);
        ArgumentNullException.ThrowIfNull(state);

        var entries = _localEntries.Value
            .Where(entry => entry.TransportKind == McpServerTransportKind.Stdio && entry.IsStartupEnabled(state))
            .ToList();
        if (entries.Count == 0)
        {
            return false;
        }

        ValidateUniqueStdioToolNames(entries);
        ConfigureServerOptions(
            serverOptions,
            CreateAggregateStdioImplementation(entries),
            CreateAggregateInstructions(entries),
            entries.SelectMany(entry => entry.Tools).Select(tool => tool.SdkTool));
        return true;
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
                            && state.IsEntryEnabled(AgentCapabilityKind.Mcp, entry.Definition.Name)
                            && entry.IsStartupEnabled(state))
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
                    entry.SourceKind,
                    true,
                    McpCapabilityMessageCode.Connectivity.ConnectedToEndpoint,
                    Stopwatch.GetElapsedTime(startedAt),
                    tools.Count,
                    testedAt,
                    tools.Select(static tool => tool.Name).OrderBy(static name => name, StringComparer.Ordinal).ToList());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new McpConnectivityTestResult(
                    entry.Definition.Name,
                    entry.SourceKind,
                    false,
                    ex.Message,
                    Stopwatch.GetElapsedTime(startedAt),
                    0,
                    testedAt);
            }
        }

        var message = entry.TransportKind == McpServerTransportKind.Http
            ? McpCapabilityMessageCode.Connectivity.LocalHttpDisplayUrlMissing
            : McpCapabilityMessageCode.Connectivity.LocalStdioInProcessReady;

        return new McpConnectivityTestResult(
            entry.Definition.Name,
            entry.SourceKind,
            true,
            message,
            Stopwatch.GetElapsedTime(startedAt),
            entry.Tools.Count,
            testedAt,
            entry.Tools.Select(static tool => tool.Name).OrderBy(static name => name, StringComparer.Ordinal).ToList());
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
                McpCapabilityMessageCode.Connectivity.ConnectedAndListedTools,
                Stopwatch.GetElapsedTime(startedAt),
                tools.Count,
                testedAt,
                tools.Select(static tool => tool.Name).OrderBy(static name => name, StringComparer.Ordinal).ToList());
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
        IReadOnlyList<Skill> skills,
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
        var activeSkills = skills
            .Where(skill => IsActiveSkillMcpServer(skill, loadedModuleKeys, logger))
            .OrderBy(skill => skill.McpServerDefinition!.Name, StringComparer.Ordinal)
            .ToList();

        ValidateUniqueSkillServerNames(activeSkills);
        ValidateNoLocalServerNameCollision(activeServers, activeSkills);

        var entries = activeServers
            .Select(server =>
            {
                var tools = McpServerToolDiscovery.Discover(server, serviceProvider, xmlDocumentationService);
                return LocalMcpServerEntry.FromServer(server, options.McpHttpEndpointPath, options.McpHttpDisplayUrl, tools);
            })
            .ToList();

        entries.AddRange(activeSkills.Select(skill =>
        {
            var tools = McpServerToolDiscovery.DiscoverSkill(skill, serviceProvider, xmlDocumentationService);
            return LocalMcpServerEntry.FromSkill(skill, options.McpHttpEndpointPath, options.McpHttpDisplayUrl, tools);
        }));

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

    private static bool IsActiveSkillMcpServer(
        Skill skill,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ILogger logger)
    {
        var mcpDefinition = skill.McpServerDefinition;
        if (mcpDefinition is null)
        {
            return false;
        }

        if (!skill.IsEnabled)
        {
            logger.LogDebug(
                "Skipping MCP exposure for disabled AI skill '{SkillName}'.",
                skill.Definition.Name);
            return false;
        }

        var missing = skill.RequiredModules
            .Where(required => !loadedModuleKeys.Contains(required))
            .ToList();

        if (missing.Count == 0)
        {
            return true;
        }

        logger.LogDebug(
            "Skipping MCP exposure for AI skill '{SkillName}' because required modules are not loaded: {RequiredModules}.",
            skill.Definition.Name,
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

    private static void ValidateUniqueSkillServerNames(IReadOnlyList<Skill> skills)
    {
        var duplicateNames = skills
            .GroupBy(skill => skill.McpServerDefinition!.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate skill-backed MCP server names are not allowed: " +
                string.Join(", ", duplicateNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) + ".");
        }
    }

    private static void ValidateNoLocalServerNameCollision(
        IReadOnlyList<MonicaMcpServer> servers,
        IReadOnlyList<Skill> skills)
    {
        var serverNames = servers
            .Select(server => server.Definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateNames = skills
            .Select(skill => skill.McpServerDefinition!.Name)
            .Where(serverNames.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "MCP server names must be unique across local servers and skill-backed servers: " +
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

    private static void ValidateUniqueStdioToolNames(IReadOnlyList<LocalMcpServerEntry> entries)
    {
        var duplicateNames = entries
            .SelectMany(entry => entry.Tools)
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            var details = duplicateNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name =>
                {
                    var servers = entries
                        .Where(entry => entry.Tools.Any(tool => string.Equals(tool.Name, name, StringComparison.Ordinal)))
                        .Select(entry => entry.Definition.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(server => server, StringComparer.OrdinalIgnoreCase);
                    return $"{name} ({string.Join(", ", servers)})";
                });

            throw new InvalidOperationException(
                "Duplicate stdio MCP server tool names are not allowed because stdio exposes one aggregate tool namespace. " +
                "Use HTTP transport for per-server URLs or rename the duplicate tools: " +
                string.Join("; ", details) + ".");
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

    private static void ConfigureServerOptions(McpServerOptions serverOptions, LocalMcpServerEntry entry)
    {
        ConfigureServerOptions(
            serverOptions,
            CreateImplementation(entry),
            entry.Definition.Instructions,
            entry.Tools.Select(tool => tool.SdkTool));
    }

    private static void ConfigureServerOptions(
        McpServerOptions serverOptions,
        Implementation implementation,
        string? instructions,
        IEnumerable<McpServerTool> tools)
    {
        serverOptions.ServerInfo = implementation;
        serverOptions.ServerInstructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions;
        serverOptions.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();

        foreach (var tool in tools)
        {
            serverOptions.ToolCollection.Add(tool);
        }
    }

    private static Implementation CreateImplementation(LocalMcpServerEntry entry)
    {
        return new Implementation
        {
            Name = entry.Definition.Name,
            Title = string.IsNullOrWhiteSpace(entry.Definition.Title) ? null : entry.Definition.Title,
            Version = entry.Definition.Version,
            Description = entry.Definition.Description,
            WebsiteUrl = entry.Definition.WebsiteUrl
        };
    }

    private static Implementation CreateAggregateStdioImplementation(IReadOnlyList<LocalMcpServerEntry> entries)
    {
        return entries.Count == 1
            ? CreateImplementation(entries[0])
            : new Implementation
            {
                Name = "monica-mcp-stdio",
                Title = "Monica MCP Stdio",
                Version = "1.0.0",
                Description = "Aggregated stdio MCP tools exposed by Monica."
            };
    }

    private static string? CreateAggregateInstructions(IReadOnlyList<LocalMcpServerEntry> entries)
    {
        var instructions = entries
            .Select(entry => entry.Definition.Instructions)
            .Where(instruction => !string.IsNullOrWhiteSpace(instruction))
            .ToList();

        return instructions.Count == 0
            ? null
            : string.Join(Environment.NewLine + Environment.NewLine, instructions);
    }

    private sealed record LocalMcpServerEntry(
        McpServerDefinition Definition,
        string? SkillName,
        bool SkillMcpEnabledByDefault,
        McpCatalogSourceKind SourceKind,
        McpServerTransportKind TransportKind,
        bool IsLocalToolEnabled,
        string HttpEndpointPath,
        string? HttpDisplayUrl,
        IReadOnlyList<McpServerToolDescriptor> Tools)
    {
        internal static LocalMcpServerEntry FromServer(
            MonicaMcpServer server,
            string httpEndpointPath,
            string? httpDisplayUrl,
            IReadOnlyList<McpServerToolDescriptor> tools)
        {
            return new LocalMcpServerEntry(
                server.Definition,
                null,
                false,
                McpCatalogSourceKind.LocalServer,
                server.TransportKind,
                server.IsLocalToolEnabled,
                ResolveHttpEndpointPath(server.TransportKind, httpEndpointPath, server.Definition.Name),
                ResolveHttpDisplayUrl(server.TransportKind, httpDisplayUrl, server.Definition.Name),
                tools);
        }

        internal static LocalMcpServerEntry FromSkill(
            Skill skill,
            string httpEndpointPath,
            string? httpDisplayUrl,
            IReadOnlyList<McpServerToolDescriptor> tools)
        {
            var skillDefinition = skill.McpServerDefinition
                                  ?? throw new InvalidOperationException(
                                      $"Skill '{skill.Definition.Name}' does not define MCP server exposure metadata.");

            return new LocalMcpServerEntry(
                new McpServerDefinition(skillDefinition.Name, skillDefinition.Description)
                {
                    Version = skillDefinition.Version,
                    Title = skillDefinition.Title ?? skill.Definition.Name,
                    Instructions = skillDefinition.Instructions ?? skill.Definition.Instructions,
                    WebsiteUrl = skillDefinition.WebsiteUrl
                },
                skill.Definition.Name,
                skillDefinition.EnabledByDefault,
                McpCatalogSourceKind.SkillServer,
                ToMcpTransportKind(skillDefinition.TransportKind),
                skillDefinition.IsLocalToolEnabled,
                ResolveHttpEndpointPath(ToMcpTransportKind(skillDefinition.TransportKind), httpEndpointPath, skillDefinition.Name),
                ResolveHttpDisplayUrl(ToMcpTransportKind(skillDefinition.TransportKind), httpDisplayUrl, skillDefinition.Name),
                tools);
        }

        internal McpCatalogEntryInfo ToInfo(AgentCapabilityState state)
        {
            var catalogEnabled = state.McpEnabled;
            var entryEnabled = state.IsEntryEnabled(AgentCapabilityKind.Mcp, Definition.Name);
            var startupEnabled = IsStartupEnabled(state);
            var disabledReason = ResolveDisabledReason(
                IsLocalToolEnabled,
                catalogEnabled,
                entryEnabled,
                startupEnabled: startupEnabled,
                sourceKind: SourceKind);
            var isAgentToolEnabled = disabledReason is null;

            return new McpCatalogEntryInfo(
                Definition.Name,
                Definition.Description,
                SourceKind,
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

        internal bool IsStartupEnabled(AgentCapabilityState state)
        {
            return SkillName is null
                   || state.IsSkillMcpServerEnabled(SkillName, SkillMcpEnabledByDefault);
        }

        private static string ResolveHttpEndpointPath(
            McpServerTransportKind transportKind,
            string httpEndpointPath,
            string serverName)
        {
            return transportKind == McpServerTransportKind.Http
                ? ModuleMcpOption.CreateHttpEndpointPath(httpEndpointPath, serverName)
                : httpEndpointPath;
        }

        private static string? ResolveHttpDisplayUrl(
            McpServerTransportKind transportKind,
            string? httpDisplayUrl,
            string serverName)
        {
            return transportKind == McpServerTransportKind.Http
                ? ModuleMcpOption.CreateHttpDisplayUrl(httpDisplayUrl, serverName)
                : httpDisplayUrl;
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
            var disabledReason = ResolveDisabledReason(
                Profile.IsAgentToolEnabled,
                catalogEnabled,
                entryEnabled,
                DiscoveryError,
                sourceKind: McpCatalogSourceKind.ExternalClient);
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
            mcpSourceKind: entry.SourceKind,
            mcpTransportKind: entry.TransportKind,
            mcpEndpointPath: entry.EndpointPath,
            mcpDisplayUrl: entry.DisplayUrl,
            mcpExternalProfile: entry.ExternalProfile,
            isUserManaged: entry.IsUserManaged,
            discoveryError: entry.DiscoveryError);
    }

    private static McpServerTransportKind ToMcpTransportKind(SkillMcpServerTransportKind transportKind)
    {
        return transportKind switch
        {
            SkillMcpServerTransportKind.Http => McpServerTransportKind.Http,
            SkillMcpServerTransportKind.Stdio => McpServerTransportKind.Stdio,
            _ => throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, null)
        };
    }

    private static string? ResolveDisabledReason(
        bool builtInEnabled,
        bool catalogEnabled,
        bool entryEnabled,
        string? discoveryError = null,
        bool startupEnabled = true,
        McpCatalogSourceKind? sourceKind = null)
    {
        if (!string.IsNullOrWhiteSpace(discoveryError))
        {
            return discoveryError;
        }

        if (!startupEnabled)
        {
            return McpCapabilityMessageCode.DisabledReason.SkillMcpExposureDisabled;
        }

        if (!builtInEnabled)
        {
            return sourceKind == McpCatalogSourceKind.SkillServer
                ? McpCapabilityMessageCode.DisabledReason.SkillServerNotAgentTool
                : McpCapabilityMessageCode.DisabledReason.NotAgentTool;
        }

        if (!catalogEnabled)
        {
            return McpCapabilityMessageCode.DisabledReason.CatalogDisabled;
        }

        return entryEnabled ? null : McpCapabilityMessageCode.DisabledReason.EntryDisabled;
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
