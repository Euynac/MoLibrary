using Microsoft.JSInterop;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Owns one lazily imported JavaScript module and serializes every invocation with teardown.
/// </summary>
/// <remarks>
/// Module imports are always observed to completion. If component or service disposal wins a race with
/// an import, the late reference is released before the operation returns.
/// </remarks>
internal sealed class JsModuleSession(
    IJSRuntime jsRuntime,
    string modulePath) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private Task? _disposeTask;
    private volatile bool _disposed;

    /// <summary>
    /// Invokes a JavaScript function from the owned module.
    /// </summary>
    internal async ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[]? args)
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var module = await GetOrImportModuleAsync();
            return await module.InvokeAsync<TValue>(identifier, args ?? []);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Invokes a JavaScript function that does not return a value.
    /// </summary>
    internal async ValueTask InvokeVoidAsync(string identifier, params object?[]? args)
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var module = await GetOrImportModuleAsync();
            await module.InvokeVoidAsync(identifier, args ?? []);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
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
            importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath);
            ObjectDisposedException.ThrowIf(_disposed, this);
            var ownedModule = importedModule
                ?? throw new InvalidOperationException($"JavaScript module import returned no reference for '{modulePath}'.");
            _module = ownedModule;
            importedModule = null;
            return ownedModule;
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

    private static async ValueTask DisposeReferenceAsync(IJSObjectReference? reference)
    {
        if (reference is null)
        {
            return;
        }

        try
        {
            await reference.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The disconnected browser circuit already released its JavaScript references.
        }
        catch (JSException)
        {
            // A destroyed browser context cannot retain a usable module reference.
        }
    }
}
