namespace Monica.UnitTests.Hosting;

/// <summary>
/// Async polling helper for tests that must wait for eventual in-memory state without using sleeps directly.
/// </summary>
public static class WaitFor
{
    /// <summary>
    /// Polls until <paramref name="predicate"/> returns <see langword="true"/> or the timeout expires.
    /// </summary>
    public static async Task UntilAsync(
        Func<CancellationToken, Task<bool>> predicate,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var delay = interval ?? TimeSpan.FromMilliseconds(50);

        while (!linkedCts.IsCancellationRequested)
        {
            if (await predicate(linkedCts.Token))
            {
                return;
            }

            await Task.Delay(delay, linkedCts.Token);
        }

        throw new TimeoutException("The wait predicate did not complete before the timeout.");
    }
}
