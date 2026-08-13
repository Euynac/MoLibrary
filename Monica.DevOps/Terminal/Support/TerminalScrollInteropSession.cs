using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Monica.DevOps.Terminal.Support;

/// <summary>
/// Owns the browser-side scroll controller for one terminal output surface.
/// </summary>
internal sealed class TerminalScrollInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const string MODULE_PATH =
        "./_content/Monica.DevOps/Terminal/Pages/UITerminalPage.razor.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _controller;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Scrolls the current terminal output to its latest line.
    /// </summary>
    internal async Task ScrollToBottomAsync(ElementReference outputRoot)
    {
        if (_disposed)
        {
            return;
        }

        var acquired = false;
        try
        {
            await _gate.WaitAsync(_lifetimeCancellation.Token);
            acquired = true;
            if (_disposed)
            {
                return;
            }

            if (_module is null)
            {
                var importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", MODULE_PATH);
                if (_disposed)
                {
                    await DisposeReferenceAsync(importedModule);
                    return;
                }

                _module = importedModule;
            }

            if (_controller is null)
            {
                var createdController = await _module.InvokeAsync<IJSObjectReference>(
                    "createTerminalScroller",
                    outputRoot);
                if (_disposed)
                {
                    await DisposeControllerAsync(createdController);
                    return;
                }

                _controller = createdController;
            }

            await _controller.InvokeVoidAsync("scrollToBottom");
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
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
        IJSObjectReference? controller;
        IJSObjectReference? module;
        try
        {
            controller = _controller;
            module = _module;
            _controller = null;
            _module = null;
        }
        finally
        {
            _gate.Release();
        }

        await DisposeControllerAsync(controller);
        await DisposeReferenceAsync(module);
    }

    private static async ValueTask DisposeControllerAsync(IJSObjectReference? controller)
    {
        if (controller is null)
        {
            return;
        }

        try
        {
            await controller.InvokeVoidAsync("shutdown");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }

        await DisposeReferenceAsync(controller);
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
