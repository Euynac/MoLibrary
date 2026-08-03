using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Monica.Markdown.Models;

namespace Monica.Markdown.UIMarkdown.Interop;

/// <summary>
/// Owns the JavaScript references and callback producers for one rendered markdown viewer.
/// </summary>
internal sealed class MarkdownViewerInteropSession(
    IJSRuntime jsRuntime,
    Func<string?, Task> hashChangedAsync)
    : IAsyncDisposable
{
    private const string MARKDOWN_LAYOUT_MODULE_PATH =
        "./_content/Monica.Markdown/js/markdown-layout.js";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _disposedCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private IJSObjectReference? _module;
    private IJSObjectReference? _viewerSession;
    private DotNetObjectReference<MarkdownViewerInteropSession>? _callbackReference;
    private int _disposeRequested;

    /// <summary>
    /// Imports the viewer module and creates the instance-scoped JavaScript session.
    /// </summary>
    /// <returns>
    /// Whether the sidebar should initially be shown, or <see langword="null" /> when disposal
    /// won the initialization race.
    /// </returns>
    internal async Task<bool?> InitializeAsync(ElementReference viewerRoot)
    {
        await _gate.WaitAsync();

        try
        {
            if (IsDisposeRequested)
            {
                return null;
            }

            if (_viewerSession is not null)
            {
                return null;
            }

            var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                MARKDOWN_LAYOUT_MODULE_PATH);
            DotNetObjectReference<MarkdownViewerInteropSession>? callbackReference = null;
            IJSObjectReference? viewerSession = null;
            var ownershipPublished = false;

            try
            {
                if (IsDisposeRequested)
                {
                    return null;
                }

                var showSidebar = await module.InvokeAsync<bool>("shouldShowSidebarByDefault");
                if (IsDisposeRequested)
                {
                    return null;
                }

                callbackReference = DotNetObjectReference.Create(this);
                viewerSession = await module.InvokeAsync<IJSObjectReference>(
                    "createMarkdownViewerSession",
                    viewerRoot,
                    callbackReference);

                if (IsDisposeRequested)
                {
                    return null;
                }

                _module = module;
                _viewerSession = viewerSession;
                _callbackReference = callbackReference;
                ownershipPublished = true;
                return showSidebar;
            }
            finally
            {
                if (!ownershipPublished)
                {
                    try
                    {
                        if (viewerSession is not null)
                        {
                            await ShutdownAndDisposeViewerAsync(viewerSession);
                        }
                    }
                    finally
                    {
                        try
                        {
                            callbackReference?.Dispose();
                        }
                        finally
                        {
                            await DisposeReferenceAsync(module);
                        }
                    }
                }
            }
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Replaces the active-heading tracker for the current rendered document.
    /// </summary>
    internal Task RefreshHeadingTrackerAsync(
        IReadOnlyList<string> headingIds,
        string? currentAnchorId)
    {
        return InvokeViewerAsync(
            viewerSession => viewerSession.InvokeVoidAsync(
                "observeActiveHeading",
                headingIds,
                currentAnchorId));
    }

    /// <summary>
    /// Highlights a search result in the current rendered document.
    /// </summary>
    internal Task<bool> HighlightSearchHitAsync(MarkdownSearchLocator locator)
    {
        return InvokeViewerAsync(
            viewerSession => viewerSession.InvokeAsync<bool>(
                "highlightSearchHit",
                locator.AnchorId,
                locator.HeadingLevel,
                locator.MatchedText,
                locator.PrefixContext,
                locator.SuffixContext),
            false);
    }

    /// <summary>
    /// Removes the active search-result highlight when the viewer is still connected.
    /// </summary>
    internal Task ClearSearchHitAsync()
    {
        return InvokeViewerAsync(
            viewerSession => viewerSession.InvokeVoidAsync("clearSearchHit"));
    }

    /// <summary>
    /// Receives active-heading changes from this viewer's JavaScript session.
    /// </summary>
    [JSInvokable]
    public async Task OnHashChangedAsync(string? anchorId)
    {
        await _gate.WaitAsync();

        try
        {
            if (!IsDisposeRequested)
            {
                await hashChangedAsync(anchorId);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            await _disposedCompletion.Task;
            return;
        }

        try
        {
            await _gate.WaitAsync();

            IJSObjectReference? viewerSession;
            DotNetObjectReference<MarkdownViewerInteropSession>? callbackReference;
            IJSObjectReference? module;

            try
            {
                viewerSession = _viewerSession;
                callbackReference = _callbackReference;
                module = _module;

                _viewerSession = null;
                _callbackReference = null;
                _module = null;
            }
            finally
            {
                _gate.Release();
            }

            // Shutdown runs outside the gate so a JavaScript callback already queued before
            // disposal can enter, observe the disposal flag, and settle before shutdown returns.
            try
            {
                if (viewerSession is not null)
                {
                    await ShutdownAndDisposeViewerAsync(viewerSession);
                }
            }
            finally
            {
                try
                {
                    callbackReference?.Dispose();
                }
                finally
                {
                    if (module is not null)
                    {
                        await DisposeReferenceAsync(module);
                    }
                }
            }
        }
        finally
        {
            _disposedCompletion.TrySetResult();
        }
    }

    private bool IsDisposeRequested => Volatile.Read(ref _disposeRequested) != 0;

    private async Task InvokeViewerAsync(
        Func<IJSObjectReference, ValueTask> invokeAsync)
    {
        await _gate.WaitAsync();

        try
        {
            if (IsDisposeRequested || _viewerSession is null)
            {
                return;
            }

            try
            {
                await invokeAsync(_viewerSession);
            }
            catch (JSDisconnectedException)
            {
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T> InvokeViewerAsync<T>(
        Func<IJSObjectReference, ValueTask<T>> invokeAsync,
        T fallback)
    {
        await _gate.WaitAsync();

        try
        {
            if (IsDisposeRequested || _viewerSession is null)
            {
                return fallback;
            }

            try
            {
                return await invokeAsync(_viewerSession);
            }
            catch (JSDisconnectedException)
            {
                return fallback;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task ShutdownAndDisposeViewerAsync(IJSObjectReference viewerSession)
    {
        try
        {
            try
            {
                await viewerSession.InvokeVoidAsync("shutdown");
            }
            catch (JSDisconnectedException)
            {
            }
        }
        finally
        {
            await DisposeReferenceAsync(viewerSession);
        }
    }

    private static async Task DisposeReferenceAsync(IJSObjectReference reference)
    {
        try
        {
            await reference.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
