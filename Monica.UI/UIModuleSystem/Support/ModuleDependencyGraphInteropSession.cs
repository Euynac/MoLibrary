using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Owns one dependency graph's JavaScript module and browser handle, including serialized render and teardown races.
/// </summary>
internal sealed class ModuleDependencyGraphInteropSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const string GRAPH_MODULE_PATH = "./_content/Monica.UI/js/module-dependency-graph.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;
    private Task? _disposeTask;
    private string? _renderIdentity;
    private bool _disposed;

    internal async Task<ModuleDependencyGraphRenderOutcome> RenderAsync(
        ElementReference element,
        object model,
        string renderIdentity,
        object callbackReference)
    {
        if (_disposed)
        {
            return ModuleDependencyGraphRenderOutcome.Cancelled;
        }

        if (string.Equals(renderIdentity, _renderIdentity, StringComparison.Ordinal))
        {
            return ModuleDependencyGraphRenderOutcome.Rendered;
        }

        var acquired = false;
        try
        {
            await _gate.WaitAsync(_lifetimeCancellation.Token);
            acquired = true;
            if (_disposed)
            {
                return ModuleDependencyGraphRenderOutcome.Cancelled;
            }

            if (_module is null)
            {
                var importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", GRAPH_MODULE_PATH);
                if (_disposed)
                {
                    await DisposeReferenceAsync(importedModule);
                    return ModuleDependencyGraphRenderOutcome.Cancelled;
                }

                _module = importedModule;
            }

            if (_handle is null)
            {
                var createdHandle = await _module.InvokeAsync<IJSObjectReference>(
                    "createGraph",
                    element,
                    model,
                    callbackReference);
                if (_disposed)
                {
                    await DisposeHandleAsync(createdHandle);
                    return ModuleDependencyGraphRenderOutcome.Cancelled;
                }

                _handle = createdHandle;
            }
            else
            {
                await _handle.InvokeVoidAsync("update", model);
            }

            _renderIdentity = renderIdentity;
            return ModuleDependencyGraphRenderOutcome.Rendered;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Component lifetime ended before interop acquired the serialized gate.
            return ModuleDependencyGraphRenderOutcome.Cancelled;
        }
        catch (JSDisconnectedException)
        {
            // Server circuit disconnected while rendering the graph.
            return ModuleDependencyGraphRenderOutcome.Cancelled;
        }
        catch (JSException)
        {
            // The browser context was destroyed while importing, creating, or updating this instance.
            return ModuleDependencyGraphRenderOutcome.Failed;
        }
        finally
        {
            if (acquired)
            {
                _gate.Release();
            }
        }
    }

    /// <summary>Invokes one command on this graph instance when its browser handle is ready.</summary>
    internal Task InvokeAsync(string command, object? argument = null) => InvokeCoreAsync(command, argument);

    private async Task InvokeCoreAsync(string command, object? argument)
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
            if (_disposed || _handle is null)
            {
                return;
            }

            if (argument is null)
            {
                await _handle.InvokeVoidAsync(command);
            }
            else
            {
                await _handle.InvokeVoidAsync(command, argument);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The graph owner ended while the command waited for the serialized handle.
        }
        catch (JSDisconnectedException)
        {
            // The Blazor circuit disconnected before the command completed.
        }
        catch (JSException)
        {
            // A destroyed browser context cannot execute graph commands.
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
        IJSObjectReference? handle;
        IJSObjectReference? module;
        try
        {
            handle = _handle;
            module = _module;
            _handle = null;
            _module = null;
        }
        finally
        {
            _gate.Release();
        }

        await DisposeHandleAsync(handle);
        await DisposeReferenceAsync(module);
        // Calls that observed the pre-disposal state may still be about to enter the gate. Keeping these lightweight
        // coordination primitives alive lets cancellation settle those races without ObjectDisposedException.
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
            // The browser circuit already owns cleanup through the per-instance MutationObserver.
        }
        catch (JSException)
        {
            // The browser context was already destroyed while releasing the reference.
        }
    }

    private static async ValueTask DisposeHandleAsync(IJSObjectReference? handle)
    {
        if (handle is null)
        {
            return;
        }

        try
        {
            await handle.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException)
        {
            // The circuit disconnected; the browser-side observer owns best-effort cleanup.
        }
        catch (JSException)
        {
            // A destroyed browser context cannot retain usable observers or listeners.
        }

        await DisposeReferenceAsync(handle);
    }
}

internal enum ModuleDependencyGraphRenderOutcome
{
    Rendered,
    Failed,
    Cancelled
}
