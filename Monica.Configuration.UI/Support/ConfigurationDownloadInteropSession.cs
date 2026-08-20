using Microsoft.JSInterop;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Owns the JavaScript download module for one configuration export dialog.
/// </summary>
internal sealed class ConfigurationDownloadInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const string MODULE_PATH = "./_content/Monica.Configuration.UI/js/configuration-ui.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Downloads text when the dialog still owns a connected browser module.
    /// </summary>
    internal async Task<bool> DownloadTextAsync(string fileName, string contentType, string content)
    {
        if (_disposed)
        {
            return false;
        }

        var acquired = false;
        try
        {
            await _gate.WaitAsync(_lifetimeCancellation.Token);
            acquired = true;
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

            await _module.InvokeVoidAsync("downloadText", fileName, contentType, content);
            return !_disposed;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (JSException) when (_disposed)
        {
            return false;
        }
        finally
        {
            if (acquired)
            {
                _gate.Release();
            }
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
        await _lifetimeCancellation.CancelAsync();
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
