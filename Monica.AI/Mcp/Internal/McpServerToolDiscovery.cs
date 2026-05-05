using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Monica.AI.Skills.Internal;
using Monica.AI.Mcp.Models;
using Monica.Core.Skills;
using Monica.Core.Skills.Annotations;
using Monica.Core.Skills.Models;
using Monica.Core.XmlDocumentation.Abstractions;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Internal;

internal static class McpServerToolDiscovery
{
    private const BindingFlags DISCOVERY_FLAGS =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static IReadOnlyList<McpServerToolDescriptor> Discover(
        MonicaMcpServer server,
        IServiceProvider serviceProvider,
        IXmlDocumentationService? xmlDocs)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return DiscoverCore(
            server,
            server.GetType(),
            server.Definition.Name,
            skillName: null,
            skillMcpEnabledByDefault: false,
            server.TransportKind,
            server.IsLocalToolEnabled,
            server.SerializerOptions,
            serviceProvider,
            xmlDocs);
    }

    internal static IReadOnlyList<McpServerToolDescriptor> DiscoverSkill(
        Skill skill,
        IServiceProvider serviceProvider,
        IXmlDocumentationService? xmlDocs)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var definition = skill.McpServerDefinition
                         ?? throw new InvalidOperationException(
                             $"Skill '{skill.Definition.Name}' does not define MCP server exposure metadata.");

        return DiscoverCore(
            skill,
            skill.GetType(),
            definition.Name,
            skill.Definition.Name,
            definition.EnabledByDefault,
            ToMcpTransportKind(definition.TransportKind),
            definition.IsLocalToolEnabled,
            skill.SerializerOptions,
            serviceProvider,
            xmlDocs);
    }

    private static IReadOnlyList<McpServerToolDescriptor> DiscoverCore(
        object targetOwner,
        Type discoveryType,
        string serverName,
        string? skillName,
        bool skillMcpEnabledByDefault,
        McpServerTransportKind transportKind,
        bool isLocalToolEnabled,
        JsonSerializerOptions? serializerOptions,
        IServiceProvider serviceProvider,
        IXmlDocumentationService? xmlDocs)
    {
        var toolMethods = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        var descriptors = new List<McpServerToolDescriptor>();

        foreach (var method in discoveryType.GetMethods(DISCOVERY_FLAGS))
        {
            var toolAttribute = method.GetCustomAttribute<SkillToolAttribute>();
            if (toolAttribute is null || toolAttribute.Disabled)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(toolAttribute.Name)
                ? SkillToolNameHelper.DeriveName(method.Name)
                : toolAttribute.Name.Trim();

            if (toolMethods.TryGetValue(name, out var existingMethod))
            {
                throw new InvalidOperationException(
                    $"MCP server '{serverName}' exposes duplicate tool name '{name}' from methods " +
                    $"'{existingMethod.Name}' and '{method.Name}'. Set an explicit [SkillTool(Name = ...)] value.");
            }

            toolMethods.Add(name, method);
            var description = SkillDescriptionResolver.ResolveMethod(method, xmlDocs);
            var target = method.IsStatic ? null : targetOwner;
            var schemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                ParameterDescriptionProvider = parameter =>
                    SkillDescriptionResolver.ResolveParameter(parameter, xmlDocs)
            };
            var metadata = new McpToolMetadata(
                serverName,
                transportKind,
                isLocalToolEnabled,
                skillName,
                skillMcpEnabledByDefault);

            var sdkTool = McpServerTool.Create(method, target, new McpServerToolCreateOptions
            {
                Services = serviceProvider,
                Name = name,
                Description = description,
                SerializerOptions = serializerOptions,
                SchemaCreateOptions = schemaCreateOptions,
                Metadata = [metadata]
            });

            descriptors.Add(new McpServerToolDescriptor(
                serverName,
                skillName,
                transportKind,
                isLocalToolEnabled,
                name,
                description,
                method,
                targetOwner,
                sdkTool,
                isLocalToolEnabled
                    ? AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
                    {
                        Name = name,
                        Description = description,
                        SerializerOptions = serializerOptions,
                        JsonSchemaCreateOptions = schemaCreateOptions
                    })
                    : null));
        }

        return descriptors;
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
