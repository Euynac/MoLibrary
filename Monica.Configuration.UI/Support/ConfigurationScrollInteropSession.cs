using Microsoft.JSInterop;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Owns the configuration editor's lazily imported scroll module for one dialog instance.
/// </summary>
internal sealed class ConfigurationScrollInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const string MODULE_PATH = "./_content/Monica.Configuration.UI/js/configuration-ui.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private Task? _disposeTask;
    private volatile bool _disposed;

    /// <summary>
    /// Scrolls the requested editor entry into view while the dialog still owns the module.
    /// </summary>
    internal async Task<bool> ScrollIntoViewAsync(string elementId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return false;
            }

            if (_module is null)
            {
                var importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", MODULE_PATH);
                if (_disposed)
                {
                    await DisposeReferenceAsync(importedModule);
                    return false;
                }

                _module = importedModule;
            }

            await _module.InvokeVoidAsync("scrollElementIntoView", elementId);
            return !_disposed;
        }
        catch (JSDisconnectedException)
        {
            return false;
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
        }
        catch (JSException)
        {
        }
    }
}
