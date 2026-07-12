namespace Monica.AI.Services.Support;

/// <summary>
/// Async-local implementation of <see cref="IAIChatRuntimeContextAccessor"/>.
/// </summary>
internal sealed class AIChatRuntimeContextAccessor : IAIChatRuntimeContextAccessor
{
    private readonly AsyncLocal<AIChatRuntimeContext?> _current = new();

    /// <inheritdoc />
    public AIChatRuntimeContext Current => _current.Value ?? AIChatRuntimeContext.Empty;

    /// <summary>
    /// Pushes a runtime context for the current async flow and restores the previous value when disposed.
    /// </summary>
    /// <param name="context">The context to expose to the current async flow.</param>
    /// <returns>A disposable scope that restores the previous context.</returns>
    public IDisposable Push(AIChatRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = _current.Value;
        _current.Value = context;
        return new RestoreScope(this, previous);
    }

    private sealed class RestoreScope(
        AIChatRuntimeContextAccessor accessor,
        AIChatRuntimeContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            accessor._current.Value = previous;
            _disposed = true;
        }
    }
}
