using System.Reflection;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Skills.Internal;

internal sealed class InlineSkillResource : AgentSkillResource
{
    private readonly AIFunction _function;

    internal InlineSkillResource(
        string name,
        MethodInfo method,
        object? target,
        string? description,
        JsonSerializerOptions? serializerOptions)
        : base(name, description)
    {
        _function = AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            SerializerOptions = serializerOptions
        });
    }

    /// <inheritdoc />
    public override async Task<object?> ReadAsync(
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _function.InvokeAsync(
                new AIFunctionArguments { Services = serviceProvider },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Error: Failed to read resource '{Name}'. {ex.GetType().Name}: {ex.Message}";
        }
    }
}
