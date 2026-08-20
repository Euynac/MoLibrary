using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Monica.AI.UI.UIChat.Support;

/// <summary>
/// Owns the JavaScript auto-scroll session for one rendered chat transcript.
/// </summary>
internal sealed class ChatMessageListInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const string MODULE_PATH =
        "./_content/Monica.AI.UI/UIChat/Components/ChatMessageList.razor.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _controller;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Initializes the transcript's browser controller once.
    /// </summary>
    internal async Task InitializeAsync(ElementReference root)
    {
        if (_disposed || _controller is not null)
        {
            return;
        }

        var acquired = false;
        IJSObjectReference? importedModule = null;
        IJSObjectReference? createdController = null;
        try
        {
            await _gate.WaitAsync(_lifetimeCancellation.Token);
            acquired = true;
            if (_disposed || _controller is not null)
            {
                return;
            }

            importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", MODULE_PATH);
            if (_disposed)
            {
                return;
            }

            createdController = await importedModule.InvokeAsync<IJSObjectReference>("initAutoScroll", root);
            if (_disposed)
            {
                return;
            }

            _module = importedModule;
            _controller = createdController;
            importedModule = null;
            createdController = null;
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
            await DisposeControllerAsync(createdController);
            await DisposeReferenceAsync(importedModule);
            if (acquired)
            {
                _gate.Release();
            }
        }
    }

    /// <summary>
    /// Scrolls to the latest message when the browser controller is still alive.
    /// </summary>
    internal Task ScrollToBottomAsync() => InvokeControllerAsync(
        controller => controller.InvokeVoidAsync("scrollToBottom").AsTask());

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task InvokeControllerAsync(Func<IJSObjectReference, Task> operation)
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
            if (_disposed || _controller is null)
            {
                return;
            }

            await operation(_controller);
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
