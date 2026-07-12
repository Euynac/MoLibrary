using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Services.Support;

/// <summary>
/// Owns one streaming producer and merges model updates with decorator-published tool updates.
/// </summary>
internal sealed class AgentStreamingCoordinator(AIChatRuntimeContextAccessor runtimeContextAccessor)
{
    internal async IAsyncEnumerable<AgentResponseUpdate> RunAsync(
        AIAgent agent,
        AgentSession session,
        ChatMessage input,
        AIChatRuntimeContext runtimeContext,
        ChatClientAgentRunOptions runOptions,
        AgentResponseUpdateChannel updateChannel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var producer = ProduceAsync(
            agent,
            session,
            input,
            runtimeContext,
            runOptions,
            updateChannel,
            cancellationToken);

        try
        {
            await foreach (var update in updateChannel.ReadAllAsync(cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            // ProduceAsync converts failures into channel completion, but its lifetime remains
            // owned and observed here when a consumer stops enumeration early.
            await producer;
        }
    }

    private async Task ProduceAsync(
        AIAgent agent,
        AgentSession session,
        ChatMessage input,
        AIChatRuntimeContext runtimeContext,
        ChatClientAgentRunOptions runOptions,
        AgentResponseUpdateChannel updateChannel,
        CancellationToken cancellationToken)
    {
        try
        {
            using (AgentResponseUpdateChannelContext.Push(updateChannel))
            using (runtimeContextAccessor.Push(runtimeContext))
            {
                await foreach (var update in agent.RunStreamingAsync(
                                   [input],
                                   session,
                                   runOptions,
                                   cancellationToken))
                {
                    await updateChannel.PublishAsync(update, cancellationToken);
                }
            }

            updateChannel.Complete();
        }
        catch (Exception ex)
        {
            updateChannel.Complete(ex);
        }
    }
}
