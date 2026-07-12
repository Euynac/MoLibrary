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
        JsonElement? arguments,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var functionArguments = CreateFunctionArguments(arguments, serviceProvider);

        try
        {
            return await _function.InvokeAsync(functionArguments, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolInvocationErrorResult.Create(Name, functionArguments, ex);
        }
    }

    private static AIFunctionArguments CreateFunctionArguments(
        JsonElement? arguments,
        IServiceProvider? serviceProvider)
    {
        if (arguments is null
            || arguments.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new AIFunctionArguments { Services = serviceProvider };
        }

        if (arguments.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Inline skill scripts expect a JSON object but received '{arguments.Value.ValueKind}'.");
        }

        var values = arguments.Value
            .EnumerateObject()
            .ToDictionary(static property => property.Name, static property => (object?)property.Value);
        return new AIFunctionArguments(values) { Services = serviceProvider };
    }
}
