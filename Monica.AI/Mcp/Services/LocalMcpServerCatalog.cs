using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.Core.Modularity.Models;
using Monica.Core.Skills;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Modules;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Services;

internal sealed class LocalMcpServerCatalog
{
    private readonly Lazy<IReadOnlyList<LocalMcpServerEntry>> _entries;

    internal LocalMcpServerCatalog(
        IEnumerable<MonicaMcpServer> localServers,
        IEnumerable<Skill> skills,
        ILoadedModuleCatalog loadedModules,
        IXmlDocumentationService xmlDocumentationService,
        IOptions<ModuleMcpOption> options,
        IServiceProvider serviceProvider,
        ILogger<LocalMcpServerCatalog> logger)
    {
        _entries = new Lazy<IReadOnlyList<LocalMcpServerEntry>>(() => BuildEntries(
            localServers.ToList(),
            skills.ToList(),
            loadedModules.GetLoadedModuleKeys(),
            options.Value,
            serviceProvider,
            xmlDocumentationService,
            logger));
    }

    internal bool HasHttpServers => _entries.Value.Any(entry => entry.TransportKind == McpServerTransportKind.Http);

    internal bool HasStdioServers => _entries.Value.Any(entry => entry.TransportKind == McpServerTransportKind.Stdio);

    internal IReadOnlyList<LocalMcpServerEntry> Entries => _entries.Value;

    internal bool TryConfigureHttpServerOptions(string? serverName, McpServerOptions serverOptions)
    {
        ArgumentNullException.ThrowIfNull(serverOptions);
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return false;
        }

        var normalizedName = serverName.Trim();
        var entry = Entries.FirstOrDefault(candidate =>
            candidate.TransportKind == McpServerTransportKind.Http
            && string.Equals(candidate.Definition.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        ConfigureServerOptions(serverOptions, entry);
        return true;
    }

    internal bool TryConfigureStdioServerOptions(McpServerOptions serverOptions, AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(serverOptions);
        ArgumentNullException.ThrowIfNull(state);

        var entries = Entries
            .Where(entry => entry.TransportKind == McpServerTransportKind.Stdio && entry.IsStartupEnabled(state))
            .ToList();
        if (entries.Count == 0)
        {
            return false;
        }

        ValidateUniqueToolNames(entries, "stdio MCP server");
        ConfigureServerOptions(
            serverOptions,
            CreateAggregateStdioImplementation(entries),
            CreateAggregateInstructions(entries),
            entries.SelectMany(entry => entry.Tools).Select(tool => tool.SdkTool));
        return true;
    }

    internal IReadOnlyList<Microsoft.Extensions.AI.AITool> GetAgentTools(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Entries
            .Where(entry => entry.IsLocalToolEnabled
                            && state.IsEntryEnabled(AgentCapabilityKind.Mcp, entry.Definition.Name)
                            && entry.IsStartupEnabled(state))
            .SelectMany(entry => entry.Tools)
            .Select(tool => tool.AgentTool)
            .OfType<Microsoft.Extensions.AI.AITool>()
            .ToList();
    }

    internal IReadOnlyList<McpCatalogEntryInfo> GetEntries(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Entries.Select(entry => entry.ToInfo(state)).ToList();
    }

    internal LocalMcpServerEntry? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Entries.FirstOrDefault(entry =>
            string.Equals(entry.Definition.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<LocalMcpServerEntry> BuildEntries(
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
        var activeSkills = skills
            .Where(skill => IsActiveSkillMcpServer(skill, loadedModuleKeys, logger))
            .OrderBy(skill => skill.McpServerDefinition!.Name, StringComparer.Ordinal)
            .ToList();

        ValidateUniqueNames(activeServers.Select(server => server.Definition.Name), "MCP server");
        ValidateUniqueNames(activeSkills.Select(skill => skill.McpServerDefinition!.Name), "skill-backed MCP server");
        ValidateNoNameCollision(activeServers, activeSkills);

        var entries = activeServers
            .Select(server => LocalMcpServerEntry.FromServer(
                server,
                options.McpHttpEndpointPath,
                options.McpHttpDisplayUrl,
                McpServerToolDiscovery.Discover(server, serviceProvider, xmlDocumentationService)))
            .ToList();

        entries.AddRange(activeSkills.Select(skill => LocalMcpServerEntry.FromSkill(
            skill,
            options.McpHttpEndpointPath,
            options.McpHttpDisplayUrl,
            McpServerToolDiscovery.DiscoverSkill(skill, serviceProvider, xmlDocumentationService))));
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

        return HasRequiredModules(
            server.Definition.Name,
            "MCP server",
            server.RequiredModules,
            loadedModuleKeys,
            logger);
    }

    private static bool IsActiveSkillMcpServer(
        Skill skill,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ILogger logger)
    {
        if (skill.McpServerDefinition is null)
        {
            return false;
        }

        if (!skill.IsEnabled)
        {
            logger.LogDebug("Skipping MCP exposure for disabled AI skill '{SkillName}'.", skill.Definition.Name);
            return false;
        }

        return HasRequiredModules(
            skill.Definition.Name,
            "AI skill MCP exposure",
            skill.RequiredModules,
            loadedModuleKeys,
            logger);
    }

    private static bool HasRequiredModules(
        string name,
        string capabilityKind,
        IEnumerable<ModuleKey> requiredModules,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ILogger logger)
    {
        var missing = requiredModules.Where(required => !loadedModuleKeys.Contains(required)).ToList();
        if (missing.Count == 0)
        {
            return true;
        }

        logger.LogDebug(
            "Skipping {CapabilityKind} '{CapabilityName}' because required modules are not loaded: {RequiredModules}.",
            capabilityKind,
            name,
            string.Join(", ", missing));
        return false;
    }

    private static void ValidateUniqueNames(IEnumerable<string> names, string entryKind)
    {
        var duplicateNames = names
            .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate {entryKind} names are not allowed: {string.Join(", ", duplicateNames)}.");
        }
    }

    private static void ValidateNoNameCollision(
        IReadOnlyList<MonicaMcpServer> servers,
        IReadOnlyList<Skill> skills)
    {
        var serverNames = servers.Select(server => server.Definition.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateNames = skills
            .Select(skill => skill.McpServerDefinition!.Name)
            .Where(serverNames.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "MCP server names must be unique across local servers and skill-backed servers: "
                + string.Join(", ", duplicateNames) + ".");
        }
    }

    private static void ValidateUniqueToolNames(IReadOnlyList<LocalMcpServerEntry> entries, string namespaceKind)
    {
        var duplicateNames = entries
            .SelectMany(entry => entry.Tools)
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (duplicateNames.Count == 0)
        {
            return;
        }

        var details = duplicateNames.Select(name =>
        {
            var servers = entries
                .Where(entry => entry.Tools.Any(tool => string.Equals(tool.Name, name, StringComparison.Ordinal)))
                .Select(entry => entry.Definition.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(server => server, StringComparer.OrdinalIgnoreCase);
            return $"{name} ({string.Join(", ", servers)})";
        });
        throw new InvalidOperationException(
            $"Duplicate {namespaceKind} tool names are not allowed because the transport exposes one aggregate tool namespace. "
            + "Use HTTP transport for per-server URLs or rename the duplicate tools: "
            + string.Join("; ", details) + ".");
    }

    private static void ConfigureServerOptions(McpServerOptions options, LocalMcpServerEntry entry)
    {
        ConfigureServerOptions(
            options,
            CreateImplementation(entry),
            entry.Definition.Instructions,
            entry.Tools.Select(tool => tool.SdkTool));
    }

    private static void ConfigureServerOptions(
        McpServerOptions options,
        Implementation implementation,
        string? instructions,
        IEnumerable<McpServerTool> tools)
    {
        options.ServerInfo = implementation;
        options.ServerInstructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions;
        options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in tools)
        {
            options.ToolCollection.Add(tool);
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
}
