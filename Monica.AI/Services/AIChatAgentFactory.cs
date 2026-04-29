using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.Services.Support;
using Monica.AI.Skills.Services;

namespace Monica.AI.Services;

/// <summary>
/// Composes chat agents from the base chat client plus registered AI context providers.
/// </summary>
public class AIChatAgentFactory(
    MonicaAgentSkillsProviderFactory skillsProviderFactory,
    IEnumerable<IAIChatAgentDecorator> agentDecorators,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
    : IAIChatAgentFactory
{
    private readonly IReadOnlyList<IAIChatAgentDecorator> _agentDecorators = agentDecorators.ToList();
    private readonly ILogger<AIChatAgentFactory> _logger = loggerFactory.CreateLogger<AIChatAgentFactory>();

    /// <inheritdoc />
    public Task<AIAgent> CreateAsync(
        IChatClient chatClient,
        AIChatAgentCreateContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var builder = new AIChatAgentBuilder(context.Instructions);
        builder.AddContextProvider(skillsProviderFactory.GetProvider());

        var agentOptions = builder.BuildOptions();

        _logger.LogInformation(
            "Creating AI chat agent with skill context provider and {DecoratorCount} agent decorators.",
            _agentDecorators.Count);

        var agent = new ChatClientAgent(
            chatClient,
            agentOptions,
            loggerFactory,
            serviceProvider);

        if (_agentDecorators.Count == 0)
        {
            return Task.FromResult<AIAgent>(agent);
        }

        var pipeline = agent.AsBuilder();
        foreach (var agentDecorator in _agentDecorators)
        {
            agentDecorator.Configure(pipeline);
        }

        var decoratedAgent = pipeline.Build(serviceProvider);
        _logger.LogInformation("Created decorated AI chat agent pipeline type: {AgentType}.", decoratedAgent.GetType().FullName);
        return Task.FromResult(decoratedAgent);
    }
}
