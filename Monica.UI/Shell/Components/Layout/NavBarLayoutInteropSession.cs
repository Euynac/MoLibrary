using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Monica.UI.Shell.Components.Layout;

/// <summary>
/// Owns the navigation bar's JavaScript module, layout observer, and .NET callback reference.
/// Initialization and teardown are serialized so late JavaScript completions cannot outlive the component.
/// </summary>
internal sealed class NavBarLayoutInteropSession(
    IJSRuntime jsRuntime,
    NavBar callbackTarget) : IAsyncDisposable
{
    internal const string LAYOUT_MODULE_PATH = "./_content/Monica.UI/js/navbar-layout.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _observer;
    private DotNetObjectReference<NavBar>? _callbackReference;
    private Task? _disposeTask;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes the browser-side layout observer once for this component instance.
    /// </summary>
    internal async Task InitializeAsync(
        ElementReference desktopNavigation,
        ElementReference measurementRail,
        int maximumVisibleCategories,
        int safetyMarginPixels)
    {
        if (_disposed || _initialized)
        {
            return;
        }

        var acquired = false;
        IJSObjectReference? importedModule = null;
        IJSObjectReference? createdObserver = null;
        DotNetObjectReference<NavBar>? createdCallbackReference = null;
        try
        {
            await _gate.WaitAsync(_lifetimeCancellation.Token);
            acquired = true;
            if (_disposed || _initialized)
            {
                return;
            }

            importedModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                LAYOUT_MODULE_PATH);
            if (_disposed)
            {
                return;
            }

            createdCallbackReference = DotNetObjectReference.Create(callbackTarget);
            createdObserver = await importedModule.InvokeAsync<IJSObjectReference>(
                "createNavBarLayoutObserver",
                desktopNavigation,
                measurementRail,
                maximumVisibleCategories,
                safetyMarginPixels,
                createdCallbackReference);
            if (_disposed)
            {
                return;
            }

            _module = importedModule;
            _observer = createdObserver;
            _callbackReference = createdCallbackReference;
            importedModule = null;
            createdObserver = null;
            createdCallbackReference = null;
            _initialized = true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The component lifetime ended before initialization acquired the serialized gate.
        }
        catch (JSDisconnectedException)
        {
            // The server circuit disconnected before the observer became usable.
        }
        finally
        {
            await DisposeOwnedReferencesAsync(
                createdObserver,
                createdCallbackReference,
                importedModule);

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
        _lifetimeCancellation.Cancel();
        await _gate.WaitAsync();
        IJSObjectReference? observer;
        IJSObjectReference? module;
        DotNetObjectReference<NavBar>? callbackReference;
        try
        {
            observer = _observer;
            module = _module;
            callbackReference = _callbackReference;
            _observer = null;
            _module = null;
            _callbackReference = null;
            _initialized = false;
        }
        finally
        {
            _gate.Release();
        }

        await DisposeOwnedReferencesAsync(observer, callbackReference, module);
    }

    private static async ValueTask DisposeOwnedReferencesAsync(
        IJSObjectReference? observer,
        DotNetObjectReference<NavBar>? callbackReference,
        IJSObjectReference? module)
    {
        try
        {
            await DisposeObserverAsync(observer);
        }
        finally
        {
            callbackReference?.Dispose();
            await DisposeReferenceAsync(module);
        }
    }

    private static async ValueTask DisposeObserverAsync(IJSObjectReference? observer)
    {
        if (observer is null)
        {
            return;
        }

        try
        {
            await observer.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException)
        {
            // The browser context already released its observer and event listeners.
        }
        catch (JSException)
        {
            // A destroyed browser context cannot retain a usable observer.
        }

        await DisposeReferenceAsync(observer);
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
            // The browser circuit already owns cleanup.
        }
        catch (JSException)
        {
            // The browser context was destroyed while releasing the reference.
        }
    }
}
