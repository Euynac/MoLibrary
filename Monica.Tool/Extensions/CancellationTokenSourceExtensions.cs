namespace Monica.Tool.Extensions;

public static class CancellationTokenSourceExtensions
{
    /// <summary>
    /// Safely cancels and disposes the CancellationTokenSource.
    /// Handles null and already-disposed cases gracefully.
    /// </summary>
    public static void SafeCancelAndDispose(this CancellationTokenSource? cts)
    {
        if (cts == null) return;
        try { cts.Cancel(); }
        catch (ObjectDisposedException) { }
        cts.Dispose();
    }

    /// <summary>
    /// Safely cancels (async) and disposes the CancellationTokenSource.
    /// Handles null and already-disposed cases gracefully.
    /// </summary>
    public static async ValueTask SafeCancelAndDisposeAsync(this CancellationTokenSource? cts)
    {
        if (cts == null) return;
        try { await cts.CancelAsync(); }
        catch (ObjectDisposedException) { }
        cts.Dispose();
    }
}
