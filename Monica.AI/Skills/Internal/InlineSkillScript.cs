using System.Reflection;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Services.Support;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal sealed class InlineSkillScript : AgentSkillScript
{
    private readonly AIFunction _function;

    internal InlineSkillScript(
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
                    SkillDescriptionResolver.ResolveParameter(parameter, xmlDocs)
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
        try
        {
            return await _function.InvokeAsync(arguments, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolInvocationErrorResult.Create(Name, arguments, ex);
        }
    }
}
