using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Tools;

/// <summary>
/// Mutable builder used by tool providers to compose a chat agent configuration.
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
    /// Add a direct tool to the agent.
    /// </summary>
    public void AddTool(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools.Add(tool);
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
    /// Allow the model to automatically choose and invoke tools.
    /// </summary>
    public void EnableAutomaticToolCalling()
    {
        ToolMode = ChatToolMode.Auto;
    }

    /// <summary>
    /// Restrict the model to a single tool call at a time for each response.
    /// </summary>
    public void DisableMultipleToolCalling()
    {
        AllowMultipleToolCalls = false;
    }

    /// <summary>
    /// Build the final agent options.
    /// </summary>
    public ChatClientAgentOptions BuildOptions()
    {
        return new ChatClientAgentOptions
        {
            ChatOptions = string.IsNullOrWhiteSpace(Instructions)
                ? null
                : new ChatOptions
                {
                    Instructions = Instructions
                },
            AIContextProviders = _contextProviders.Count > 0 ? [.. _contextProviders] : null
        };
    }

    /// <summary>
    /// Build run-time chat options that must be applied per invocation.
    /// </summary>
    public ChatOptions? BuildRuntimeChatOptions()
    {
        if (_tools.Count == 0 && ToolMode is null)
        {
            return null;
        }

        return new ChatOptions
        {
            Tools = _tools.Count > 0 ? [.. _tools] : null,
            ToolMode = ToolMode,
            AllowMultipleToolCalls = AllowMultipleToolCalls
        };
    }

    private ChatToolMode? ToolMode { get; set; }
    private bool? AllowMultipleToolCalls { get; set; }
}
