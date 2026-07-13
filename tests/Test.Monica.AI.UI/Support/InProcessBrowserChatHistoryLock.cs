using System.Collections.Concurrent;
using Monica.AI.UI.UIChat.Providers.Browser;

namespace Test.Monica.AI.UI.Support;

internal sealed class InProcessBrowserChatHistoryLock : IBrowserChatHistoryLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string scope,
        CancellationToken ct = default)
    {
        var gate = _gates.GetOrAdd(scope, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
