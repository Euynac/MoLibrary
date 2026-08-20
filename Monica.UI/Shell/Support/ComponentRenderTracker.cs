namespace Monica.UI.Shell.Support;

/// <summary>
/// Tracks renderer callbacks raised by synchronous service events so component teardown can await them.
/// </summary>
internal sealed class ComponentRenderTracker
{
    private readonly List<Task> _pendingRenders = [];
    private readonly object _sync = new();
    private bool _completed;

    /// <summary>
    /// Queues and retains a renderer callback until <see cref="CompleteAsync"/> observes it.
    /// </summary>
    internal void Queue(Func<Task> renderCallback)
    {
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            try
            {
                _pendingRenders.Add(renderCallback());
            }
            catch (Exception exception)
            {
                _pendingRenders.Add(Task.FromException(exception));
            }
        }
    }

    /// <summary>
    /// Stops accepting callbacks and observes all callbacks already queued.
    /// </summary>
    internal async ValueTask CompleteAsync()
    {
        Task[] pendingRenders;
        lock (_sync)
        {
            _completed = true;
            pendingRenders = _pendingRenders.ToArray();
            _pendingRenders.Clear();
        }

        try
        {
            await Task.WhenAll(pendingRenders);
        }
        catch (ObjectDisposedException)
        {
            // The renderer was released between event dispatch and component teardown.
        }
        catch (InvalidOperationException)
        {
            // A disconnected renderer cannot service a queued StateHasChanged callback.
        }
    }
}
