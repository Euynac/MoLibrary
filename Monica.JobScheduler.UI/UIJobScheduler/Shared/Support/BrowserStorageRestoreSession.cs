namespace Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

/// <summary>
/// Owns and drains one component's first-render browser-storage restoration.
/// </summary>
internal sealed class BrowserStorageRestoreSession : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _sync = new();
    private Task? _restoreTask;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Starts the restore operation once and supplies a token that is canceled when the component is disposed.
    /// </summary>
    /// <param name="restore">The browser-storage restore operation.</param>
    /// <returns>The tracked restore task.</returns>
    internal Task RunAsync(Func<CancellationToken, Task> restore)
    {
        ArgumentNullException.ThrowIfNull(restore);

        lock (_sync)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            return _restoreTask ??= restore(_lifetimeCancellation.Token);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            _disposed = true;
            _disposeTask = DisposeCoreAsync(_restoreTask);
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync(Task? restoreTask)
    {
        try
        {
            await _lifetimeCancellation.CancelAsync();
            if (restoreTask is not null)
            {
                await restoreTask;
            }
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }
}
