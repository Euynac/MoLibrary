using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;
using Monica.Core.Results;
using Monica.Markdown.Facades;
using Monica.Markdown.Localization;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Dialogs;
using Monica.Markdown.UIMarkdown.Models;
using Monica.Tool.Algorithms.Trees;
using Monica.UI.Shared.Components.Markdown;

namespace Monica.Markdown.UIMarkdown.State;

/// <summary>
/// Owns the transient UI state and page orchestration for the markdown viewer route.
/// </summary>
public sealed class MarkdownViewerPageState(
    MarkdownFacade markdownFacade,
    IDialogService dialogService,
    NavigationManager navigationManager,
    ISnackbar snackbar,
    IJSRuntime jsRuntime,
    IStringLocalizer<MarkdownResource> localizer)
    : IAsyncDisposable
{
    private readonly DialogOptions _groupSwitcherDialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseButton = true,
        CloseOnEscapeKey = true
    };

    private readonly DialogOptions _documentSearchDialogOptions = new()
    {
        MaxWidth = MaxWidth.ExtraLarge,
        FullWidth = true,
        CloseButton = true,
        CloseOnEscapeKey = true
    };

    private Func<Task>? _renderRequestedAsync;
    private bool _isAttached;
    private IJSObjectReference? _hashObserverModule;
    private IJSObjectReference? _layoutModule;
    private DotNetObjectReference<MarkdownViewerPageState>? _hashObserverReference;
    private bool _pendingHeadingTrackerRefresh;
    private PendingSearchNavigation? _pendingSearchNavigation;
    private bool _pendingSearchHighlightClear;

    /// <summary>
    /// Whether the page is loading its document groups.
    /// </summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Whether the current document content is loading.
    /// </summary>
    public bool IsContentLoading { get; private set; }

    /// <summary>
    /// Whether the sidebar is visible.
    /// </summary>
    public bool ShowSidebar { get; private set; }

    /// <summary>
    /// Whether the table of contents rail is visible.
    /// </summary>
    public bool ShowTableOfContents { get; private set; } = true;

    /// <summary>
    /// All registered markdown groups.
    /// </summary>
    public List<MarkdownDocumentGroup>? Groups { get; private set; }

    /// <summary>
    /// The currently selected group key.
    /// </summary>
    public string? SelectedGroupKey { get; private set; }

    /// <summary>
    /// The loaded tree for the active group.
    /// </summary>
    public TreeNode<MarkdownDocumentNodeData>? CurrentTree { get; private set; }

    /// <summary>
    /// The currently selected document.
    /// </summary>
    public MarkdownDocument? SelectedDocument { get; private set; }

    /// <summary>
    /// The raw markdown content of the selected document.
    /// </summary>
    public string? DocumentContent { get; private set; }

    /// <summary>
    /// The headings reported by the markdown renderer.
    /// </summary>
    public IReadOnlyList<MoMarkdownHeading> DocumentHeadings { get; private set; } = [];

    /// <summary>
    /// The current anchor id reflected by navigation or heading observation.
    /// </summary>
    public string? CurrentAnchorId { get; private set; }

    /// <summary>
    /// The active group resolved from the current selection.
    /// </summary>
    public MarkdownDocumentGroup? CurrentGroup =>
        Groups?.FirstOrDefault(group => string.Equals(group.Key, SelectedGroupKey, StringComparison.Ordinal));

    /// <summary>
    /// CSS class applied to the main workspace.
    /// </summary>
    public string WorkspaceClass =>
        ShowSidebar ? "doc-workspace" : "doc-workspace sidebar-hidden";

    /// <summary>
    /// CSS class applied to the main reader stage.
    /// </summary>
    public string MainStageClass =>
        ShowTableOfContents ? "doc-main-stage" : "doc-main-stage toc-hidden";

    /// <summary>
    /// Attaches the page render callback and starts observing navigation changes.
    /// </summary>
    public void Attach(Func<Task> renderRequestedAsync)
    {
        _renderRequestedAsync = renderRequestedAsync;

        if (_isAttached)
        {
            return;
        }

        navigationManager.LocationChanged += OnLocationChanged;
        _isAttached = true;
    }

    /// <summary>
    /// Runs page initialization and post-render coordination.
    /// </summary>
    public async Task HandleAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _layoutModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Monica.Markdown/js/markdown-layout.js");
            ShowSidebar = await _layoutModule.InvokeAsync<bool>("shouldShowSidebarByDefault");

            await LoadGroupsAsync();

            _hashObserverModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Monica.UI/js/mo-url-hash-observer.js");
            _hashObserverReference = DotNetObjectReference.Create(this);
            await _hashObserverModule.InvokeVoidAsync("observeHash", _hashObserverReference);
        }

        if (_pendingHeadingTrackerRefresh)
        {
            await RefreshHeadingTrackerAsync();
        }

        if (_pendingSearchHighlightClear)
        {
            await ClearSearchHitAsync();
        }

        if (_pendingSearchNavigation is not null && CanApplySearchNavigation(_pendingSearchNavigation))
        {
            await ApplySearchHitAsync(_pendingSearchNavigation);
        }
    }

    /// <summary>
    /// Opens the knowledge base switcher dialog.
    /// </summary>
    public async Task OpenGroupSwitcherAsync()
    {
        if (Groups is not { Count: > 0 } groups)
        {
            return;
        }

        var parameters = new DialogParameters<DocumentGroupSwitcherDialog>
        {
            { x => x.Groups, groups },
            { x => x.SelectedGroupKey, SelectedGroupKey }
        };

        var dialog = await dialogService.ShowAsync<DocumentGroupSwitcherDialog>(
            localizer["MarkdownViewer:Dialogs:SwitchKnowledgeBaseTitle"],
            parameters,
            _groupSwitcherDialogOptions);

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: string selectedGroupKey }
            || string.Equals(selectedGroupKey, SelectedGroupKey, StringComparison.Ordinal))
        {
            return;
        }

        await OnGroupSelectedAsync(selectedGroupKey);
    }

    /// <summary>
    /// Opens the document search dialog.
    /// </summary>
    public async Task OpenDocumentSearchAsync()
    {
        if (Groups is not { Count: > 0 })
        {
            return;
        }

        var parameters = new DialogParameters<DocumentSearchDialog>
        {
            { x => x.CurrentGroup, CurrentGroup }
        };

        var dialog = await dialogService.ShowAsync<DocumentSearchDialog>(
            localizer["MarkdownViewer:Dialogs:Search:Title"],
            parameters,
            _documentSearchDialogOptions);

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: MarkdownDocumentSearchResult searchResult })
        {
            return;
        }

        await OpenSearchResultAsync(searchResult);
    }

    /// <summary>
    /// Handles a new group selection from the sidebar or switcher dialog.
    /// </summary>
    public async Task OnGroupSelectedAsync(string groupKey)
    {
        ResetSearchHighlight();
        CurrentAnchorId = null;
        DocumentHeadings = [];
        _pendingHeadingTrackerRefresh = true;
        await LoadGroupTreeAsync(groupKey);
        NavigateToLocation(new MarkdownViewerLocation(groupKey));
    }

    /// <summary>
    /// Handles a document selection from the document tree.
    /// </summary>
    public async Task OnDocumentSelectedAsync(MarkdownDocument? document)
    {
        ResetSearchHighlight();
        CurrentAnchorId = null;
        DocumentHeadings = [];
        _pendingHeadingTrackerRefresh = true;
        await LoadDocumentAsync(document);
        NavigateToLocation(new MarkdownViewerLocation(SelectedGroupKey, document?.RelativePath));
    }

    /// <summary>
    /// Toggles the sidebar rail.
    /// </summary>
    public void ToggleSidebar()
    {
        ShowSidebar = !ShowSidebar;
    }

    /// <summary>
    /// Toggles the table of contents rail.
    /// </summary>
    public void ToggleTableOfContents()
    {
        ShowTableOfContents = !ShowTableOfContents;
    }

    /// <summary>
    /// Updates the heading list reported by the markdown renderer.
    /// </summary>
    public Task OnDocumentHeadingsChangedAsync(IReadOnlyList<MoMarkdownHeading> headings)
    {
        var normalizedHeadings = headings
            .Where(static heading => heading.Level <= 3)
            .ToArray();

        if (DocumentHeadings.SequenceEqual(normalizedHeadings))
        {
            return Task.CompletedTask;
        }

        DocumentHeadings = normalizedHeadings;
        CurrentAnchorId = ResolveCurrentAnchorId(CurrentAnchorId);
        _pendingHeadingTrackerRefresh = true;
        return NotifyStateChangedAsync();
    }

    /// <summary>
    /// Navigates to a heading inside the active document.
    /// </summary>
    public Task OnHeadingSelectedAsync(string anchorId)
    {
        if (SelectedDocument is null || string.IsNullOrWhiteSpace(anchorId))
        {
            return Task.CompletedTask;
        }

        CurrentAnchorId = anchorId;
        NavigateToLocation(new MarkdownViewerLocation(SelectedGroupKey, SelectedDocument.RelativePath, anchorId));
        return NotifyStateChangedAsync();
    }

    /// <summary>
    /// Receives hash updates from the shared hash observer JavaScript module.
    /// </summary>
    [JSInvokable]
    public Task OnHashChangedAsync(string? anchorId)
    {
        var normalizedAnchorId = ResolveCurrentAnchorId(anchorId);
        if (string.Equals(CurrentAnchorId, normalizedAnchorId, StringComparison.Ordinal))
        {
            return NotifyStateChangedAsync();
        }

        CurrentAnchorId = normalizedAnchorId;
        return NotifyStateChangedAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isAttached)
        {
            navigationManager.LocationChanged -= OnLocationChanged;
            _isAttached = false;
        }

        if (_hashObserverModule is not null && _hashObserverReference is not null)
        {
            try
            {
                await _hashObserverModule.InvokeVoidAsync("unobserveHash", _hashObserverReference);
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _hashObserverReference?.Dispose();

        if (_hashObserverModule is not null)
        {
            try
            {
                await _hashObserverModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        if (_layoutModule is not null)
        {
            try
            {
                await _layoutModule.InvokeVoidAsync("clearSearchHit");
            }
            catch (JSDisconnectedException)
            {
            }

            try
            {
                await _layoutModule.InvokeVoidAsync("disposeActiveHeadingTracker");
            }
            catch (JSDisconnectedException)
            {
            }

            try
            {
                await _layoutModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private async Task LoadGroupsAsync()
    {
        IsLoading = true;
        await NotifyStateChangedAsync();

        try
        {
            if ((await markdownFacade.GetAllDocumentGroupsAsync())
                .IsFailed(out var error, out var groups))
            {
                snackbar.Add(error, Severity.Error);
                return;
            }

            Groups = groups;
            await ApplyLocationAsync(MarkdownViewerLocation.FromAbsoluteUri(navigationManager.Uri));
        }
        catch (Exception ex)
        {
            snackbar.Add(localizer["MarkdownViewer:Messages:LoadDataFailed", ex.Message], Severity.Error);
        }
        finally
        {
            IsLoading = false;
            await NotifyStateChangedAsync();
        }
    }

    private async Task OpenSearchResultAsync(MarkdownDocumentSearchResult result)
    {
        var navigation = new PendingSearchNavigation(Guid.NewGuid().ToString("N"), result);
        _pendingSearchNavigation = navigation;
        _pendingSearchHighlightClear = false;

        var location = new MarkdownViewerLocation(
            result.GroupKey,
            result.DocumentRelativePath,
            result.AnchorId);
        var targetUri = location.ToRelativeUri();
        var currentUri = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);

        if (string.Equals(targetUri, currentUri, StringComparison.Ordinal))
        {
            await ApplyLocationAsync(location);
            return;
        }

        NavigateToLocation(location);
    }

    private async Task ApplyLocationAsync(MarkdownViewerLocation location)
    {
        var requestedAnchorId = ResolveCurrentAnchorId(location.AnchorId);
        CurrentAnchorId = requestedAnchorId;

        if (Groups is not { Count: > 0 })
        {
            return;
        }

        var targetGroup = ResolveTargetGroup(location.GroupKey);
        if (targetGroup is null)
        {
            ResetSearchHighlight();
            ClearDocumentSelection();
            await NotifyStateChangedAsync();
            return;
        }

        if (!string.Equals(SelectedGroupKey, targetGroup.Key, StringComparison.Ordinal)
            || (CurrentTree is null && targetGroup.IsValid))
        {
            await LoadGroupTreeAsync(targetGroup.Key, requestedAnchorId);
        }

        if (string.IsNullOrWhiteSpace(location.DocumentRelativePath))
        {
            ResetSearchHighlight();
            ClearDocumentSelection();
            await NotifyStateChangedAsync();
            return;
        }

        var targetDocument = FindDocumentByRelativePath(CurrentTree, location.DocumentRelativePath);
        if (targetDocument is null)
        {
            ResetSearchHighlight();
            ClearDocumentSelection();
            await NotifyStateChangedAsync();
            return;
        }

        var isCurrentDocument = SelectedDocument is not null
                                && string.Equals(
                                    SelectedDocument.RelativePath,
                                    targetDocument.RelativePath,
                                    StringComparison.OrdinalIgnoreCase);

        if (!isCurrentDocument || DocumentContent is null)
        {
            DocumentHeadings = [];
            await LoadDocumentAsync(targetDocument, requestedAnchorId);
            return;
        }

        CurrentAnchorId = requestedAnchorId;
        await NotifyStateChangedAsync();
    }

    private async Task LoadGroupTreeAsync(string groupKey, string? requestedAnchorId = null)
    {
        SelectedGroupKey = groupKey;
        ClearDocumentSelection();
        CurrentAnchorId = requestedAnchorId;
        CurrentTree = null;

        var group = CurrentGroup;
        if (group is null || !group.IsValid)
        {
            await NotifyStateChangedAsync();
            return;
        }

        if ((await markdownFacade.GetDocumentTreeAsync(groupKey))
            .IsFailed(out var error, out var tree))
        {
            snackbar.Add(error, Severity.Error);
            return;
        }

        CurrentTree = tree;
        await NotifyStateChangedAsync();
    }

    private async Task LoadDocumentAsync(MarkdownDocument? document, string? requestedAnchorId = null)
    {
        SelectedDocument = document;
        DocumentContent = null;
        CurrentAnchorId = requestedAnchorId;

        if (document is null)
        {
            IsContentLoading = false;
            await NotifyStateChangedAsync();
            return;
        }

        IsContentLoading = true;
        await NotifyStateChangedAsync();

        try
        {
            if ((await markdownFacade.GetDocumentContentAsync(document))
                .IsFailed(out var error, out var content))
            {
                snackbar.Add(error, Severity.Error);
                return;
            }

            DocumentContent = content;
        }
        finally
        {
            IsContentLoading = false;
            await NotifyStateChangedAsync();
        }
    }

    private MarkdownDocumentGroup? ResolveTargetGroup(string? groupKey)
    {
        if (Groups is not { Count: > 0 })
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(groupKey))
        {
            var matchedGroup = Groups.FirstOrDefault(group =>
                string.Equals(group.Key, groupKey, StringComparison.Ordinal));
            if (matchedGroup is not null)
            {
                return matchedGroup;
            }
        }

        if (!string.IsNullOrWhiteSpace(SelectedGroupKey) && CurrentGroup is not null)
        {
            return CurrentGroup;
        }

        return Groups.FirstOrDefault(static group => group.IsValid) ?? Groups[0];
    }

    private static MarkdownDocument? FindDocumentByRelativePath(
        TreeNode<MarkdownDocumentNodeData>? node,
        string? relativePath)
    {
        if (node is null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        foreach (var child in node.Children)
        {
            if (child.Data.Document is not null
                && string.Equals(
                    child.Data.Document.RelativePath,
                    relativePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child.Data.Document;
            }

            var nestedDocument = FindDocumentByRelativePath(child, relativePath);
            if (nestedDocument is not null)
            {
                return nestedDocument;
            }
        }

        return null;
    }

    private void ClearDocumentSelection()
    {
        SelectedDocument = null;
        DocumentContent = null;
        IsContentLoading = false;
        CurrentAnchorId = null;
        DocumentHeadings = [];
        _pendingHeadingTrackerRefresh = true;
    }

    private async Task RefreshHeadingTrackerAsync()
    {
        if (_layoutModule is null)
        {
            return;
        }

        _pendingHeadingTrackerRefresh = false;

        if (SelectedDocument is null || DocumentHeadings.Count == 0)
        {
            await _layoutModule.InvokeVoidAsync("disposeActiveHeadingTracker");
            return;
        }

        await _layoutModule.InvokeVoidAsync(
            "observeActiveHeading",
            DocumentHeadings.Select(static heading => heading.Id).ToArray(),
            CurrentAnchorId,
            _hashObserverReference);
    }

    private void ResetSearchHighlight()
    {
        _pendingSearchNavigation = null;
        _pendingSearchHighlightClear = true;
    }

    private bool CanApplySearchNavigation(PendingSearchNavigation navigation)
    {
        return _layoutModule is not null
               && !IsContentLoading
               && SelectedDocument is not null
               && DocumentContent is not null
               && string.Equals(SelectedDocument.GroupKey, navigation.Result.GroupKey, StringComparison.Ordinal)
               && string.Equals(
                   SelectedDocument.RelativePath,
                   navigation.Result.DocumentRelativePath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplySearchHitAsync(PendingSearchNavigation navigation)
    {
        if (_layoutModule is null)
        {
            return;
        }

        _pendingSearchNavigation = null;

        try
        {
            await _layoutModule.InvokeAsync<bool>(
                "highlightSearchHit",
                navigation.Result.Locator.AnchorId,
                navigation.Result.Locator.HeadingLevel,
                navigation.Result.Locator.MatchedText,
                navigation.Result.Locator.PrefixContext,
                navigation.Result.Locator.SuffixContext);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task ClearSearchHitAsync()
    {
        if (_layoutModule is null)
        {
            return;
        }

        _pendingSearchHighlightClear = false;

        try
        {
            await _layoutModule.InvokeVoidAsync("clearSearchHit");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private string? ResolveCurrentAnchorId(string? anchorId)
    {
        var normalizedAnchorId = NormalizeAnchorId(anchorId);
        if (normalizedAnchorId is null || DocumentHeadings.Count == 0)
        {
            return normalizedAnchorId;
        }

        foreach (var candidate in GetAnchorCandidates(normalizedAnchorId))
        {
            var matchedHeading = DocumentHeadings.FirstOrDefault(
                heading => string.Equals(heading.Id, candidate, StringComparison.Ordinal));
            if (matchedHeading is not null)
            {
                return matchedHeading.Id;
            }
        }

        return normalizedAnchorId;
    }

    private static IEnumerable<string> GetAnchorCandidates(string anchorId)
    {
        yield return anchorId;

        string? decodedAnchorId = null;
        try
        {
            decodedAnchorId = Uri.UnescapeDataString(anchorId);
        }
        catch (UriFormatException)
        {
        }

        if (!string.IsNullOrEmpty(decodedAnchorId)
            && !string.Equals(decodedAnchorId, anchorId, StringComparison.Ordinal))
        {
            yield return decodedAnchorId;

            var encodedDecodedAnchorId = WebUtility.UrlEncode(decodedAnchorId);
            if (!string.Equals(encodedDecodedAnchorId, anchorId, StringComparison.Ordinal))
            {
                yield return encodedDecodedAnchorId;
            }
        }
    }

    private static string? NormalizeAnchorId(string? anchorId)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            return null;
        }

        var normalizedAnchorId = anchorId.Trim();
        if (normalizedAnchorId.StartsWith('#'))
        {
            normalizedAnchorId = normalizedAnchorId[1..];
        }

        return string.IsNullOrWhiteSpace(normalizedAnchorId) ? null : normalizedAnchorId;
    }

    private void NavigateToLocation(MarkdownViewerLocation location)
    {
        var targetUri = location.ToRelativeUri();
        var currentUri = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);

        if (string.Equals(targetUri, currentUri, StringComparison.Ordinal))
        {
            return;
        }

        navigationManager.NavigateTo(targetUri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _ = ApplyLocationAsync(MarkdownViewerLocation.FromAbsoluteUri(e.Location));
    }

    private Task NotifyStateChangedAsync()
    {
        return _renderRequestedAsync?.Invoke() ?? Task.CompletedTask;
    }

    private sealed record PendingSearchNavigation(string RequestKey, MarkdownDocumentSearchResult Result);
}
