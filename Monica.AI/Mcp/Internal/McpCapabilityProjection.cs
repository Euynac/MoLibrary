using System.Text.Json;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Internal;

internal static class McpCapabilityProjection
{
    internal static AgentCapabilityEntryInfo ToCapabilityInfo(McpCatalogEntryInfo entry)
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

    internal static string? ResolveDisabledReason(
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
