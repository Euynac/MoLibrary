namespace Monica.Configuration.Services.Support;

/// <summary>
/// Serializes provider reloads with operations that must observe one stable runtime configuration view.
/// </summary>
internal sealed class ConfigurationRuntimeSnapshotLock
{
    private static readonly SemaphoreSlim PROCESS_RUNTIME_LOCK = new(1, 1);

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await PROCESS_RUNTIME_LOCK.WaitAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            PROCESS_RUNTIME_LOCK.Release();
        }
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await PROCESS_RUNTIME_LOCK.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            PROCESS_RUNTIME_LOCK.Release();
        }
    }
}
