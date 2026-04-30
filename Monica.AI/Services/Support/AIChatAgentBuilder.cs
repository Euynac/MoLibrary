using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Services.Support;

/// <summary>
/// Mutable builder used to compose chat agent construction options.
/// </summary>
public sealed class AIChatAgentBuilder(string? instructions)
{
    private readonly List<AIContextProvider> _contextProviders = [];
    private readonly List<AITool> _tools = [];
    private readonly List<string> _instructions = string.IsNullOrWhiteSpace(instructions)
        ? []
        : [instructions];

    /// <summary>
    /// System instructions for the agent.
    /// </summary>
    public string? Instructions => _instructions.Count > 0
        ? string.Join(Environment.NewLine + Environment.NewLine, _instructions)
        : null;

    /// <summary>
    /// Add an AI context provider to the agent.
    /// </summary>
    public void AddContextProvider(AIContextProvider contextProvider)
    {
        ArgumentNullException.ThrowIfNull(contextProvider);
        _contextProviders.Add(contextProvider);
    }

    /// <summary>
    /// Add tools that should be available for the whole agent lifetime.
    /// </summary>
    public void AddTools(IEnumerable<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools.AddRange(tools);
    }

    /// <summary>
    /// Append additional system instructions to the agent.
    /// </summary>
    public void AppendInstructions(string instructionsText)
    {
        if (string.IsNullOrWhiteSpace(instructionsText))
        {
            return;
        }

        _instructions.Add(instructionsText);
    }

    /// <summary>
    /// Build the final agent options.
    /// </summary>
    public ChatClientAgentOptions BuildOptions()
    {
        return new ChatClientAgentOptions
        {
            ChatOptions = string.IsNullOrWhiteSpace(Instructions) && _tools.Count == 0
                ? null
                : new ChatOptions
                {
                    Instructions = Instructions,
                    Tools = _tools.Count > 0 ? [.. _tools] : null
                },
            AIContextProviders = _contextProviders.Count > 0 ? [.. _contextProviders] : null
        };
    }
}
