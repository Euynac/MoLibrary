using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Monica.AI.UI.UIChat.Components;

namespace Monica.AI.UI.UIChat.Support;

/// <summary>
/// Owns the JavaScript module, keyboard controller, and callback reference for one chat composer.
/// </summary>
internal sealed class ChatComposerInteropSession(
    IJSRuntime jsRuntime,
    ChatInputArea callbackTarget) : IAsyncDisposable
{
    internal const string MODULE_PATH =
        "./_content/Monica.AI.UI/UIChat/Components/ChatInputArea.razor.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _controller;
    private DotNetObjectReference<ChatInputArea>? _callbackReference;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Initializes the instance-scoped browser controller once.
    /// </summary>
    internal async Task InitializeAsync(
        ElementReference root,
        string value,
        string placeholder,
        IReadOnlyDictionary<string, string> referenceIcons)
    {
        if (_disposed || _controller is not null)
        {
            return;
        }

        var acquired = false;
        IJSObjectReference? importedModule = null;
        IJSObjectReference? createdController = null;
        DotNetObjectReference<ChatInputArea>? createdCallbackReference = null;
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

            createdCallbackReference = DotNetObjectReference.Create(callbackTarget);
            createdController = await importedModule.InvokeAsync<IJSObjectReference>(
                "initComposerKeyboard",
                root,
                createdCallbackReference,
                value,
                placeholder,
                referenceIcons);
            if (_disposed)
            {
                return;
            }

            _module = importedModule;
            _controller = createdController;
            _callbackReference = createdCallbackReference;
            importedModule = null;
            createdController = null;
            createdCallbackReference = null;
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
            await DisposeOwnedReferencesAsync(createdController, createdCallbackReference, importedModule);
            if (acquired)
            {
                _gate.Release();
            }
        }
    }

    /// <summary>
    /// Synchronizes the rendered editor when its controller is available.
    /// </summary>
    internal Task SynchronizeAsync(
        bool pickerOpen,
        bool disabled,
        string value,
        int caretIndex,
        bool focus,
        bool force)
    {
        return InvokeControllerAsync(async controller =>
        {
            await controller.InvokeVoidAsync("setPickerOpen", pickerOpen);
            await controller.InvokeVoidAsync("setDisabled", disabled);
            await controller.InvokeVoidAsync("setValue", value, caretIndex, focus, force);
        });
    }

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
        DotNetObjectReference<ChatInputArea>? callbackReference;
        try
        {
            controller = _controller;
            module = _module;
            callbackReference = _callbackReference;
            _controller = null;
            _module = null;
            _callbackReference = null;
        }
        finally
        {
            _gate.Release();
        }

        await DisposeOwnedReferencesAsync(controller, callbackReference, module);
    }

    private static async ValueTask DisposeOwnedReferencesAsync(
        IJSObjectReference? controller,
        DotNetObjectReference<ChatInputArea>? callbackReference,
        IJSObjectReference? module)
    {
        try
        {
            if (controller is not null)
            {
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
        }
        finally
        {
            callbackReference?.Dispose();
            await DisposeReferenceAsync(module);
        }
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
