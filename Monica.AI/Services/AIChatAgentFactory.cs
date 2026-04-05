using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.Services.Support;

namespace Monica.AI.Services;

/// <summary>
/// Composes chat agents from the base chat client plus registered tool providers.
/// </summary>
public class AIChatAgentFactory(
    IEnumerable<IAIChatToolProvider> toolProviders,
    IEnumerable<IAIChatAgentDecorator> agentDecorators,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
    : IAIChatAgentFactory
{
    private readonly IReadOnlyList<IAIChatToolProvider> _toolProviders = toolProviders.ToList();
    private readonly IReadOnlyList<IAIChatAgentDecorator> _agentDecorators = agentDecorators.ToList();
    private readonly ILogger<AIChatAgentFactory> _logger = loggerFactory.CreateLogger<AIChatAgentFactory>();

    /// <inheritdoc />
    public async Task<AIAgent> CreateAsync(
        IChatClient chatClient,
        AIChatAgentCreateContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(context);

        var builder = new AIChatAgentBuilder(context.Instructions);

        foreach (var toolProvider in _toolProviders)
        {
            await toolProvider.ConfigureAsync(builder, context, ct);
        }

        var agentOptions = builder.BuildOptions();
        var runChatOptions = builder.BuildRuntimeChatOptions();
        var runtimeToolCount = runChatOptions?.Tools?.Count ?? 0;

        _logger.LogInformation(
            "Creating AI chat agent with {ToolProviderCount} tool providers, {DecoratorCount} agent decorators, and {RuntimeToolCount} runtime tools.",
            _toolProviders.Count,
            _agentDecorators.Count,
            runtimeToolCount);

        var agent = new ChatClientAgent(
            chatClient,
            agentOptions,
            loggerFactory,
            serviceProvider);

        if (_agentDecorators.Count == 0 && runChatOptions is null)
        {
            return agent;
        }

        var pipeline = agent.AsBuilder();
        if (runChatOptions is not null)
        {
            pipeline.UseConfiguredRunChatOptions(runChatOptions);
        }

        foreach (var agentDecorator in _agentDecorators)
        {
            agentDecorator.Configure(pipeline);
        }

        var decoratedAgent = pipeline.Build(serviceProvider);
        _logger.LogInformation("Created decorated AI chat agent pipeline type: {AgentType}.", decoratedAgent.GetType().FullName);
        return decoratedAgent;
    }
}
