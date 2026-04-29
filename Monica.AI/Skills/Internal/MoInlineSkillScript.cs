using System.Reflection;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal sealed class MoInlineSkillScript : AgentSkillScript
{
    private readonly AIFunction _function;

    internal MoInlineSkillScript(
        string name,
        MethodInfo method,
        object? target,
        string? description,
        JsonSerializerOptions? serializerOptions,
        IXmlDocumentationService? xmlDocs)
        : base(name, description)
    {
        _function = AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            SerializerOptions = serializerOptions,
            JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                ParameterDescriptionProvider = parameter =>
                    MoAIDescriptionResolver.ResolveParameter(parameter, xmlDocs)
            }
        });
    }

    /// <inheritdoc />
    public override JsonElement? ParametersSchema => _function.JsonSchema;

    /// <inheritdoc />
    public override async Task<object?> RunAsync(
        AgentSkill skill,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken = default)
    {
        return await _function.InvokeAsync(arguments, cancellationToken);
    }
}
