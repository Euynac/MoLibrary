using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;

namespace Monica.AI.UI.UIChat.Providers.Browser;

internal sealed class BrowserChatHistoryWebLock(IJSRuntime jsRuntime)
    : IBrowserChatHistoryLock, IAsyncDisposable
{
    private const string MODULE_PATH = "./_content/Monica.AI.UI/js/browser-chat-history-lock.js";
    private const string LOCK_PREFIX = "monica-ai-chat-history-";

    private readonly SemaphoreSlim _moduleGate = new(1, 1);
    private IJSObjectReference? _module;

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string scope,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var module = await GetModuleAsync(ct);
        var signal = new BrowserLockSignal();
        var reference = DotNetObjectReference.Create(signal);
        var holdTask = module.InvokeVoidAsync(
                "acquireExclusive",
                ct,
                BuildLockName(scope),
                reference)
            .AsTask();

        try
        {
            var completed = await Task.WhenAny(signal.Acquired, holdTask).WaitAsync(ct);
            if (completed == holdTask)
            {
                await holdTask;
                throw new InvalidOperationException("The browser history lock ended before it was acquired.");
            }

            await signal.Acquired;
            return new BrowserLockLease(signal, reference, holdTask);
        }
        catch
        {
            signal.Release();
            reference.Dispose();
            throw;
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken ct)
    {
        if (_module is not null)
        {
            return _module;
        }

        await _moduleGate.WaitAsync(ct);
        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ct, MODULE_PATH);
            return _module;
        }
        finally
        {
            _moduleGate.Release();
        }
    }

    private static string BuildLockName(string scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scope));
        return $"{LOCK_PREFIX}{Convert.ToHexString(hash)}";
    }

    public async ValueTask DisposeAsync()
    {
        _moduleGate.Dispose();
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit already owns teardown.
            }
        }

        GC.SuppressFinalize(this);
    }

    private sealed class BrowserLockSignal
    {
        private readonly TaskCompletionSource _acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Acquired => _acquired.Task;

        [JSInvokable]
        public async Task HoldAsync()
        {
            _acquired.TrySetResult();
            await _released.Task;
        }

        internal void Release() => _released.TrySetResult();
    }

    private sealed class BrowserLockLease(
        BrowserLockSignal signal,
        DotNetObjectReference<BrowserLockSignal> reference,
        Task holdTask) : IAsyncDisposable
    {
        private bool _disposed;

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            signal.Release();
            try
            {
                await holdTask;
            }
            catch (JSDisconnectedException)
            {
                // The lock disappears with the disconnected browser context.
            }
            catch (OperationCanceledException)
            {
                // Cancellation can end the interop wait after the lock callback has been released.
            }
            finally
            {
                reference.Dispose();
            }
        }
    }
}
