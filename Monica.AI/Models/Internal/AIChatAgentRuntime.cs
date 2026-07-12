using Microsoft.Agents.AI;

namespace Monica.AI.Models.Internal;

/// <summary>
/// Owns one composed agent and the disposable context providers created with it.
/// </summary>
internal sealed class AIChatAgentRuntime(AIAgent agent, IEnumerable<IDisposable> ownedResources)
    : IAsyncDisposable, IDisposable
{
    private readonly IReadOnlyList<IDisposable> _ownedResources = ownedResources.Distinct().ToList();
    private bool _disposed;

    internal AIAgent Agent { get; } = agent;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (var index = _ownedResources.Count - 1; index >= 0; index--)
        {
            _ownedResources[index].Dispose();
        }

        if (Agent is IDisposable disposableAgent && !_ownedResources.Contains(disposableAgent))
        {
            disposableAgent.Dispose();
        }

        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        for (var index = _ownedResources.Count - 1; index >= 0; index--)
        {
            _ownedResources[index].Dispose();
        }

        if (Agent is IAsyncDisposable asyncDisposableAgent)
        {
            await asyncDisposableAgent.DisposeAsync();
        }
        else if (Agent is IDisposable disposableAgent && !_ownedResources.Contains(disposableAgent))
        {
            disposableAgent.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
