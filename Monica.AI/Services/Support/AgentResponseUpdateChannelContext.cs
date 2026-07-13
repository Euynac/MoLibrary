namespace Monica.AI.Services.Support;

/// <summary>
/// Stores the current response update channel for the active async flow.
/// </summary>
internal static class AgentResponseUpdateChannelContext
{
    private static readonly AsyncLocal<AgentResponseUpdateChannel?> _current = new();

    /// <summary>
    /// Gets the current channel for the active async flow.
    /// </summary>
    public static AgentResponseUpdateChannel? Current => _current.Value;

    /// <summary>
    /// Push a channel into the current async flow and restore the previous value on dispose.
    /// </summary>
    public static IDisposable Push(AgentResponseUpdateChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return new Scope(channel);
    }

    private sealed class Scope : IDisposable
    {
        private readonly AgentResponseUpdateChannel? _previous;
        private bool _disposed;

        public Scope(AgentResponseUpdateChannel channel)
        {
            _previous = _current.Value;
            _current.Value = channel;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _current.Value = _previous;
            _disposed = true;
        }
    }
}
