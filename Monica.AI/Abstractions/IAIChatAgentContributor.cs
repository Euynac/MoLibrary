using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Services.Support;

namespace Monica.AI.Abstractions;

/// <summary>
/// Contributes optional instructions, tools, or context providers when a Monica chat agent is composed.
/// </summary>
/// <remarks>
/// Implementations are registered as singleton services and must not retain the per-agent
/// <see cref="AIChatAgentContributionContext"/> instance after the call completes.
/// </remarks>
public interface IAIChatAgentContributor
{
    /// <summary>
    /// Adds this contributor's capabilities to one agent composition.
    /// </summary>
    /// <param name="context">The current composition context.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous discovery.</param>
    ValueTask ContributeAsync(
        AIChatAgentContributionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Mutable composition surface shared with <see cref="IAIChatAgentContributor"/> implementations.
/// </summary>
public sealed class AIChatAgentContributionContext
{
    private readonly AIChatAgentBuilder _builder;

    internal AIChatAgentContributionContext(
        AIChatAgentBuilder builder,
        AgentCapabilityState capabilityState)
    {
        _builder = builder;
        CapabilityState = capabilityState;
    }

    /// <summary>
    /// Gets the persisted capability state used for this agent instance.
    /// </summary>
    public AgentCapabilityState CapabilityState { get; }

    /// <summary>
    /// Adds a context provider and transfers its lifetime to the composed agent runtime.
    /// </summary>
    public void AddContextProvider(AIContextProvider provider) => _builder.AddContextProvider(provider);

    /// <summary>
    /// Adds tools that are available for the lifetime of the composed agent.
    /// </summary>
    public void AddTools(IEnumerable<AITool> tools) => _builder.AddTools(tools);

    /// <summary>
    /// Appends additional system instructions separated from existing instructions by a blank line.
    /// </summary>
    public void AppendInstructions(string instructions) => _builder.AppendInstructions(instructions);

    /// <summary>
    /// Adds a narrowly scoped rule that auto-approves a trusted tool call. Rules must return
    /// <see langword="false"/> for calls they do not own so later rules and user approval still apply.
    /// </summary>
    public void AddToolAutoApprovalRule(Func<FunctionCallContent, ValueTask<bool>> rule)
        => _builder.AddToolAutoApprovalRule(rule);
}
