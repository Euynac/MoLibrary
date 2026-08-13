using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Monica.AI.UI.UIRAG.Support;

/// <summary>
/// Owns matched-chunk scrolling interop for one chunk viewer dialog.
/// </summary>
internal sealed class ChunkViewerInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const string MODULE_PATH = "./_content/Monica.AI.UI/UIRAG/Components/ChunkViewerDialog.razor.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private Task? _disposeTask;
    private volatile bool _disposed;

    /// <summary>
    /// Scrolls the currently matched chunk into view while the dialog remains active.
    /// </summary>
    internal async Task<bool> ScrollToMatchedChunkAsync(ElementReference viewerRoot)
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

            await _module.InvokeVoidAsync("scrollToMatchedChunk", viewerRoot);
            return !_disposed;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (JSException)
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
