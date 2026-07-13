using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.Models.Internal;
using Monica.AI.Services.Support;

namespace Test.Monica.AI.Support;

internal sealed class TestAIChatAgentFactory(
    Func<IChatClient, AIChatAgentCreateContext, CancellationToken, Task<AIChatAgentRuntime>> create)
    : IAIChatAgentFactory
{
    public Task<AIChatAgentRuntime> CreateAsync(
        IChatClient chatClient,
        AIChatAgentCreateContext context,
        CancellationToken ct = default)
        => create(chatClient, context, ct);
}
