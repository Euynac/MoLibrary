using Microsoft.JSInterop;

namespace Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

/// <summary>
/// Owns one cron helper module for a component and serializes invocations with teardown.
/// </summary>
internal sealed class CronInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const string MODULE_PATH = "./_content/Monica.JobScheduler.UI/js/cron-helper.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private Task? _disposeTask;
    private volatile bool _disposed;

    internal async ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[]? args)
    {
        await _gate.WaitAsync();
        try
        {
            ThrowIfDisposed();
            var module = await GetOrImportModuleAsync();
            return await module.InvokeAsync<TValue>(identifier, args ?? []);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async ValueTask<IJSObjectReference> GetOrImportModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        IJSObjectReference? importedModule = null;
        try
        {
            importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", MODULE_PATH);
            ThrowIfDisposed();
            _module = importedModule;
            importedModule = null;
            return _module;
        }
        finally
        {
            await DisposeReferenceAsync(importedModule);
        }
    }

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        await _gate.WaitAsync();
        IJSObjectReference? module;
        try
        {
            module = _module;
            _module = null;
        }
        finally
        {
            _gate.Release();
        }

        await DisposeReferenceAsync(module);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new OperationCanceledException("The cron interop session is shutting down.");
        }
    }

    private static async ValueTask DisposeReferenceAsync(IJSObjectReference? module)
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The disconnected circuit has already released its JavaScript references.
        }
        catch (JSException)
        {
            // A destroyed browser context cannot retain a usable module reference.
        }
    }
}
