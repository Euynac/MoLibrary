namespace Monica.AI.Services.Support;

/// <summary>
/// Stores the current response update channel for the active async flow.
/// </summary>
internal sealed class AgentResponseUpdateChannelContext
{
    private readonly AsyncLocal<AgentResponseUpdateChannel?> _current = new();

    /// <summary>
    /// Gets the current channel for the active async flow.
    /// </summary>
    public AgentResponseUpdateChannel? Current => _current.Value;

    /// <summary>
    /// Push a channel into the current async flow and restore the previous value on dispose.
    /// </summary>
    public IDisposable Push(AgentResponseUpdateChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return new Scope(this, channel);
    }

    private sealed class Scope : IDisposable
    {
        private readonly AgentResponseUpdateChannel? _previous;
        private bool _disposed;

        public Scope(AgentResponseUpdateChannelContext context, AgentResponseUpdateChannel channel)
        {
            Context = context;
            _previous = context._current.Value;
            context._current.Value = channel;
        }

        private AgentResponseUpdateChannelContext Context { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Context._current.Value = _previous;
            _disposed = true;
        }
    }
}
