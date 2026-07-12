using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Mcp.Models;
using Monica.Core.Skills;
using Monica.Core.Skills.Models;
using Monica.Modules;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Internal;

internal sealed record LocalMcpServerEntry(
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
        var transportKind = ToMcpTransportKind(skillDefinition.TransportKind);

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
            transportKind,
            skillDefinition.IsLocalToolEnabled,
            ResolveHttpEndpointPath(transportKind, httpEndpointPath, skillDefinition.Name),
            ResolveHttpDisplayUrl(transportKind, httpDisplayUrl, skillDefinition.Name),
            tools);
    }

    internal McpCatalogEntryInfo ToInfo(AgentCapabilityState state)
    {
        var catalogEnabled = state.McpEnabled;
        var entryEnabled = state.IsEntryEnabled(AgentCapabilityKind.Mcp, Definition.Name);
        var startupEnabled = IsStartupEnabled(state);
        var disabledReason = McpCapabilityProjection.ResolveDisabledReason(
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

    private static McpServerTransportKind ToMcpTransportKind(SkillMcpServerTransportKind transportKind)
    {
        return transportKind switch
        {
            SkillMcpServerTransportKind.Http => McpServerTransportKind.Http,
            SkillMcpServerTransportKind.Stdio => McpServerTransportKind.Stdio,
            _ => throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, null)
        };
    }
}
