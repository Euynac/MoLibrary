using System.Reflection;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Monica.AI.Skills.Internal;
using Monica.Core.Skills.Annotations;
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

        var discoveryType = server.GetType();
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
                    $"MCP server '{server.Definition.Name}' exposes duplicate tool name '{name}' from methods " +
                    $"'{existingMethod.Name}' and '{method.Name}'. Set an explicit [SkillTool(Name = ...)] value.");
            }

            toolMethods.Add(name, method);
            var description = SkillDescriptionResolver.ResolveMethod(method, xmlDocs);
            var target = method.IsStatic ? null : server;
            var schemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                ParameterDescriptionProvider = parameter =>
                    SkillDescriptionResolver.ResolveParameter(parameter, xmlDocs)
            };
            var metadata = new McpToolMetadata(
                server.Definition.Name,
                server.TransportKind,
                server.IsLocalToolEnabled);

            var sdkTool = McpServerTool.Create(method, target, new McpServerToolCreateOptions
            {
                Services = serviceProvider,
                Name = name,
                Description = description,
                SerializerOptions = server.SerializerOptions,
                SchemaCreateOptions = schemaCreateOptions,
                Metadata = [metadata]
            });

            descriptors.Add(new McpServerToolDescriptor(
                server.Definition.Name,
                server.TransportKind,
                server.IsLocalToolEnabled,
                name,
                description,
                method,
                server,
                sdkTool,
                server.IsLocalToolEnabled
                    ? AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
                    {
                        Name = name,
                        Description = description,
                        SerializerOptions = server.SerializerOptions,
                        JsonSchemaCreateOptions = schemaCreateOptions
                    })
                    : null));
        }

        return descriptors;
    }
}
