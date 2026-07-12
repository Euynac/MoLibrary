using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.Models.Internal;
using Monica.AI.Services.Support;

namespace Monica.AI.Services;

/// <summary>
/// Composes chat agents from the base chat client plus registered AI context providers.
/// </summary>
internal sealed class AIChatAgentFactory(
    IEnumerable<IAIChatAgentContributor> contributors,
    IEnumerable<IAIChatAgentDecorator> agentDecorators,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
    : IAIChatAgentFactory
{
    private readonly IReadOnlyList<IAIChatAgentContributor> _contributors = contributors.ToList();
    private readonly IReadOnlyList<IAIChatAgentDecorator> _agentDecorators = agentDecorators.ToList();
    private readonly ILogger<AIChatAgentFactory> _logger = loggerFactory.CreateLogger<AIChatAgentFactory>();

    /// <inheritdoc />
    public async Task<AIChatAgentRuntime> CreateAsync(
        IChatClient chatClient,
        AIChatAgentCreateContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var builder = new AIChatAgentBuilder(context.Instructions);
        var contributionContext = new AIChatAgentContributionContext(builder, context.CapabilityState);
        foreach (var contributor in _contributors)
        {
            await contributor.ContributeAsync(contributionContext, ct);
        }

        var agentOptions = builder.BuildOptions();

        _logger.LogInformation(
            "Creating AI chat agent with {ContributorCount} capability contributors and {DecoratorCount} decorators.",
            _contributors.Count,
            _agentDecorators.Count);

        var agent = new ChatClientAgent(
            chatClient,
            agentOptions,
            loggerFactory,
            serviceProvider);

        var pipeline = agent.AsBuilder();
        foreach (var agentDecorator in _agentDecorators)
        {
            agentDecorator.Configure(pipeline);
        }

#pragma warning disable AGENTS001
        pipeline.UseToolApproval(new ToolApprovalAgentOptions
        {
            AutoApprovalRules = builder.GetToolAutoApprovalRules()
        });
#pragma warning restore AGENTS001

        var decoratedAgent = pipeline.Build(serviceProvider);
        _logger.LogInformation("Created decorated AI chat agent pipeline type: {AgentType}.", decoratedAgent.GetType().FullName);
        return new AIChatAgentRuntime(decoratedAgent, builder.GetOwnedResources());
    }
}
