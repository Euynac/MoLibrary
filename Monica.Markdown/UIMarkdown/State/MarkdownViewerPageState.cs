using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using Monica.Core.Results;
using Monica.Markdown.Facades;
using Monica.Markdown.Localization;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Dialogs;
using Monica.Markdown.UIMarkdown.Interop;
using Monica.Markdown.UIMarkdown.Models;
using Monica.Modules;
using Monica.UI.Shared.Components.Markdown;
using Monica.Tool.Algorithms.Trees;

namespace Monica.Markdown.UIMarkdown.State;

/// <summary>
/// Creates markdown viewer state whose asynchronous lifetime is owned by the rendering page.
/// </summary>
internal sealed class MarkdownViewerPageStateFactory(
    MarkdownFacade markdownFacade,
    IDialogService dialogService,
    NavigationManager navigationManager,
    ISnackbar snackbar,
    IJSRuntime jsRuntime,
    IStringLocalizer<MarkdownResource> localizer,
    IOptions<ModuleLocalizationOption> localizationOptions)
{
    /// <summary>
    /// Creates and attaches a state instance to its owning component.
    /// </summary>
    internal MarkdownViewerPageState Create(Func<Task> renderRequestedAsync)
    {
        var state = new MarkdownViewerPageState(
            markdownFacade,
            dialogService,
            navigationManager,
            snackbar,
            jsRuntime,
            localizer,
            localizationOptions);

        state.Attach(renderRequestedAsync);
        return state;
    }
}

/// <summary>
/// Owns the transient UI state and page orchestration for the markdown viewer route.
/// </summary>
internal sealed class MarkdownViewerPageState(
    MarkdownFacade markdownFacade,
    IDialogService dialogService,
    NavigationManager navigationManager,
    ISnackbar snackbar,
    IJSRuntime jsRuntime,
    IStringLocalizer<MarkdownResource> localizer,
    IOptions<ModuleLocalizationOption> localizationOptions)
    : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly TaskCompletionSource _groupsReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _locationOperationsLock = new();
    private readonly List<Task> _locationOperations = [];
    private readonly TaskCompletionSource _disposedCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ModuleLocalizationOption _localizationOption = localizationOptions.Value;
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
    private MarkdownViewerInteropSession? _interopSession;
    private CancellationTokenSource? _activeLocationCancellation;
    private Task? _initializationTask;
    private bool _pendingHeadingTrackerRefresh;
    private PendingSearchNavigation? _pendingSearchNavigation;
    private bool _pendingSearchHighlightClear;
    private string? _currentTreeCulture;
    private int _disposeRequested;

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
    /// The currently selected document culture.
    /// </summary>
    public string? SelectedCulture { get; private set; }

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
    /// Persistent state describing a document missing from the selected language.
    /// </summary>
    public MarkdownMissingTranslationState? MissingTranslation { get; private set; }

    /// <summary>
    /// The active group resolved from the current selection.
    /// </summary>
    public MarkdownDocumentGroup? CurrentGroup =>
        Groups?.FirstOrDefault(group => string.Equals(group.Key, SelectedGroupKey, StringComparison.Ordinal));

    /// <summary>
    /// The resolved active language within the current group.
    /// </summary>
    public MarkdownDocumentLanguage? CurrentLanguage =>
        CurrentGroup?.ResolveLanguage(SelectedCulture);

    /// <summary>
    /// Languages available for the current group.
    /// </summary>
    public IReadOnlyList<MarkdownDocumentLanguage> CurrentLanguages =>
        CurrentGroup?.Languages ?? Array.Empty<MarkdownDocumentLanguage>();

    /// <summary>
    /// Whether the current group should expose the language switcher.
    /// </summary>
    public bool ShowLanguageSwitcher =>
        CurrentGroup is { IsMultilingual: true } group && group.Languages.Count > 1;

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
        ObjectDisposedException.ThrowIf(IsDisposeRequested, this);

        _renderRequestedAsync = renderRequestedAsync;

        if (_isAttached)
        {
            return;
        }

        navigationManager.LocationChanged += OnLocationChanged;
        _interopSession = new MarkdownViewerInteropSession(jsRuntime, OnHashChangedAsync);
        _isAttached = true;
    }

    /// <summary>
    /// Loads non-DOM page data and queues the current route through the navigation coordinator.
    /// </summary>
    public Task InitializeAsync()
    {
        if (IsDisposeRequested)
        {
            return Task.CompletedTask;
        }

        return _initializationTask ??= InitializeCoreAsync();
    }

    /// <summary>
    /// Initializes and synchronizes DOM-bound behavior after the viewer has rendered.
    /// </summary>
    public async Task HandleAfterRenderAsync(ElementReference viewerRoot, bool firstRender)
    {
        if (IsDisposeRequested || _interopSession is null)
        {
            return;
        }

        if (firstRender)
        {
            var showSidebar = await _interopSession.InitializeAsync(viewerRoot);
            if (!IsDisposeRequested && showSidebar is not null)
            {
                ShowSidebar = showSidebar.Value;
                await NotifyStateChangedAsync();
            }
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
        if (IsDisposeRequested || Groups is not { Count: > 0 } groups)
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

        if (IsDisposeRequested)
        {
            return;
        }

        var result = await dialog.Result;
        if (IsDisposeRequested
            || result is not { Canceled: false, Data: string selectedGroupKey }
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
        if (IsDisposeRequested || Groups is not { Count: > 0 })
        {
            return;
        }

        var parameters = new DialogParameters<DocumentSearchDialog>
        {
            { x => x.CurrentGroup, CurrentGroup },
            { x => x.CurrentCulture, SelectedCulture }
        };

        var dialog = await dialogService.ShowAsync<DocumentSearchDialog>(
            localizer["MarkdownViewer:Dialogs:Search:Title"],
            parameters,
            _documentSearchDialogOptions);

        if (IsDisposeRequested)
        {
            return;
        }

        var result = await dialog.Result;
        if (IsDisposeRequested
            || result is not { Canceled: false, Data: MarkdownDocumentSearchResult searchResult })
        {
            return;
        }

        await OpenSearchResultAsync(searchResult);
    }

    /// <summary>
    /// Handles a new group selection from the sidebar or switcher dialog.
    /// </summary>
    public Task OnGroupSelectedAsync(string groupKey)
    {
        if (IsDisposeRequested)
        {
            return Task.CompletedTask;
        }

        ResetSearchHighlight();
        ClearMissingTranslation();
        CurrentAnchorId = null;
        DocumentHeadings = [];
        _pendingHeadingTrackerRefresh = true;
        var targetGroup = ResolveTargetGroup(groupKey);
        var targetCulture = ResolveGroupCulture(targetGroup, SelectedCulture);
        if (targetGroup?.IsMultilingual == true)
        {
            SelectedCulture = targetCulture;
        }

        NavigateToLocation(new MarkdownViewerLocation(groupKey, Culture: SelectedCulture));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a document selection from the document tree.
    /// </summary>
    public Task OnDocumentSelectedAsync(MarkdownDocument? document)
    {
        if (IsDisposeRequested)
        {
            return Task.CompletedTask;
        }

        ResetSearchHighlight();
        ClearMissingTranslation();
        CurrentAnchorId = null;
        DocumentHeadings = [];
        _pendingHeadingTrackerRefresh = true;
        NavigateToLocation(new MarkdownViewerLocation(
            SelectedGroupKey,
            document?.NavigationRelativePath,
            Culture: SelectedCulture));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles switching to another document language within the current group.
    /// </summary>
    public Task OnLanguageSelectedAsync(string culture)
    {
        if (IsDisposeRequested
            || CurrentGroup is not { IsMultilingual: true }
            || string.IsNullOrWhiteSpace(culture)
            || string.Equals(SelectedCulture, culture, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        ResetSearchHighlight();
        var targetAnchorId = SelectedDocument is not null ? CurrentAnchorId : null;
        CurrentAnchorId = null;
        DocumentHeadings = [];
        _pendingHeadingTrackerRefresh = true;

        var targetDocumentRelativePath =
            SelectedDocument?.NavigationRelativePath
            ?? MissingTranslation?.DocumentRelativePath;

        NavigateToLocation(new MarkdownViewerLocation(
            SelectedGroupKey,
            targetDocumentRelativePath,
            targetAnchorId,
            culture));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Toggles the sidebar rail.
    /// </summary>
    public void ToggleSidebar()
    {
        if (IsDisposeRequested)
        {
            return;
        }

        ShowSidebar = !ShowSidebar;
    }

    /// <summary>
    /// Toggles the table of contents rail.
    /// </summary>
    public void ToggleTableOfContents()
    {
        if (IsDisposeRequested)
        {
            return;
        }

        ShowTableOfContents = !ShowTableOfContents;
    }

    /// <summary>
    /// Updates the heading list reported by the markdown renderer.
    /// </summary>
    public Task OnDocumentHeadingsChangedAsync(IReadOnlyList<MoMarkdownHeading> headings)
    {
        if (IsDisposeRequested)
        {
            return Task.CompletedTask;
        }

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
        if (IsDisposeRequested
            || SelectedDocument is null
            || string.IsNullOrWhiteSpace(anchorId))
        {
            return Task.CompletedTask;
        }

        CurrentAnchorId = anchorId;
        NavigateToLocation(new MarkdownViewerLocation(
            SelectedGroupKey,
            SelectedDocument.NavigationRelativePath,
            anchorId,
            SelectedCulture));
        return NotifyStateChangedAsync();
    }

    /// <summary>
    /// Receives hash updates from this viewer's instance-scoped JavaScript session.
    /// </summary>
    public Task OnHashChangedAsync(string? anchorId)
    {
        if (IsDisposeRequested)
        {
            return Task.CompletedTask;
        }

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
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            await _disposedCompletion.Task;
            return;
        }

        try
        {
            _lifetimeCancellation.Cancel();
            _renderRequestedAsync = null;

            if (_isAttached)
            {
                navigationManager.LocationChanged -= OnLocationChanged;
                _isAttached = false;
            }

            Task[] locationOperations;
            lock (_locationOperationsLock)
            {
                _activeLocationCancellation?.Cancel();
                locationOperations = _locationOperations.ToArray();
            }

            _groupsReady.TrySetCanceled(_lifetimeCancellation.Token);

            var pendingOperations = _initializationTask is null
                ? locationOperations
                : [.. locationOperations, _initializationTask];

            var interopSession = _interopSession;
            _interopSession = null;

            try
            {
                if (pendingOperations.Length > 0)
                {
                    await Task.WhenAll(pendingOperations);
                }
            }
            finally
            {
                if (interopSession is not null)
                {
                    await interopSession.DisposeAsync();
                }
            }
        }
        finally
        {
            _lifetimeCancellation.Dispose();
            _disposedCompletion.TrySetResult();
        }
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await LoadGroupsAsync(_lifetimeCancellation.Token);
            _lifetimeCancellation.Token.ThrowIfCancellationRequested();
            _groupsReady.TrySetResult();
            QueueLocation(MarkdownViewerLocation.FromAbsoluteUri(navigationManager.Uri));
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        await NotifyStateChangedAsync();

        try
        {
            var response = await markdownFacade.GetAllDocumentGroupsAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (response.IsFailed(out var error, out var groups))
            {
                snackbar.Add(error, Severity.Error);
                return;
            }

            Groups = groups;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsDisposeRequested)
            {
                snackbar.Add(localizer["MarkdownViewer:Messages:LoadDataFailed", ex.Message], Severity.Error);
            }
        }
        finally
        {
            if (!IsDisposeRequested)
            {
                IsLoading = false;
                await NotifyStateChangedAsync();
            }
        }
    }

    private Task OpenSearchResultAsync(MarkdownDocumentSearchResult result)
    {
        if (IsDisposeRequested)
        {
            return Task.CompletedTask;
        }

        var navigation = new PendingSearchNavigation(result);
        _pendingSearchNavigation = navigation;
        _pendingSearchHighlightClear = false;

        var location = new MarkdownViewerLocation(
            result.GroupKey,
            result.DocumentRelativePath,
            result.AnchorId,
            SelectedCulture);
        var targetUri = location.ToRelativeUri();
        var currentUri = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);

        if (string.Equals(targetUri, currentUri, StringComparison.Ordinal))
        {
            QueueLocation(location);
            return Task.CompletedTask;
        }

        NavigateToLocation(location);
        return Task.CompletedTask;
    }

    private async Task ApplyLocationAsync(
        MarkdownViewerLocation location,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestedAnchorId = ResolveCurrentAnchorId(location.AnchorId);
        CurrentAnchorId = requestedAnchorId;
        SelectedCulture = ResolveGlobalCulturePreference(location.Culture);

        if (Groups is not { Count: > 0 })
        {
            return;
        }

        var targetGroup = ResolveTargetGroup(location.GroupKey);
        if (targetGroup is null)
        {
            ResetSearchHighlight();
            ClearMissingTranslation();
            ClearDocumentSelection();
            await NotifyStateChangedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        var activeCulture = ResolveGroupCulture(targetGroup, location.Culture);
        if (targetGroup.IsMultilingual)
        {
            SelectedCulture = activeCulture;
        }

        if (!string.Equals(SelectedGroupKey, targetGroup.Key, StringComparison.Ordinal)
            || CurrentTree is null
            || !string.Equals(_currentTreeCulture, activeCulture, StringComparison.OrdinalIgnoreCase))
        {
            await LoadGroupTreeAsync(
                targetGroup.Key,
                activeCulture,
                requestedAnchorId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (targetGroup.HasConfigurationError)
        {
            ClearMissingTranslation();
            ClearDocumentSelection();
            await NotifyStateChangedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        if (string.IsNullOrWhiteSpace(location.DocumentRelativePath))
        {
            ResetSearchHighlight();
            ClearMissingTranslation();
            ClearDocumentSelection();
            await NotifyStateChangedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        var targetDocument = targetGroup.FindDocument(location.DocumentRelativePath, activeCulture);
        if (targetDocument is null)
        {
            ResetSearchHighlight();
            ClearDocumentSelection();

            if (targetGroup.IsMultilingual && !string.IsNullOrWhiteSpace(activeCulture))
            {
                SetMissingTranslation(location.DocumentRelativePath, activeCulture);
            }
            else
            {
                ClearMissingTranslation();
            }

            await NotifyStateChangedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        ClearMissingTranslation();

        var isCurrentDocument = SelectedDocument is not null
                                && string.Equals(
                                    SelectedDocument.FilePath,
                                    targetDocument.FilePath,
                                    StringComparison.OrdinalIgnoreCase);

        if (!isCurrentDocument || DocumentContent is null)
        {
            DocumentHeadings = [];
            await LoadDocumentAsync(targetDocument, requestedAnchorId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        CurrentAnchorId = requestedAnchorId;
        await NotifyStateChangedAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task LoadGroupTreeAsync(
        string groupKey,
        string? culture,
        string? requestedAnchorId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedGroupKey = groupKey;
        ClearDocumentSelection();
        ClearMissingTranslation();
        CurrentAnchorId = requestedAnchorId;
        CurrentTree = null;
        _currentTreeCulture = null;

        var group = CurrentGroup;
        if (group is null || group.HasConfigurationError)
        {
            await NotifyStateChangedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        var response = await markdownFacade.GetDocumentTreeAsync(groupKey, culture);
        cancellationToken.ThrowIfCancellationRequested();

        if (response.IsFailed(out var error, out var tree))
        {
            snackbar.Add(error, Severity.Error);
            return;
        }

        CurrentTree = tree;
        _currentTreeCulture = group.IsMultilingual ? culture : null;
        await NotifyStateChangedAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task LoadDocumentAsync(
        MarkdownDocument? document,
        string? requestedAnchorId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedDocument = document;
        DocumentContent = null;
        CurrentAnchorId = requestedAnchorId;

        if (document is null)
        {
            IsContentLoading = false;
            await NotifyStateChangedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        IsContentLoading = true;
        await NotifyStateChangedAsync();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var response = await markdownFacade.GetDocumentContentAsync(document);
            cancellationToken.ThrowIfCancellationRequested();

            if (response.IsFailed(out var error, out var content))
            {
                snackbar.Add(error, Severity.Error);
                return;
            }

            DocumentContent = content;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested && !IsDisposeRequested)
            {
                IsContentLoading = false;
                await NotifyStateChangedAsync();
            }
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

        return Groups.FirstOrDefault(static group => !group.HasConfigurationError)
               ?? Groups.FirstOrDefault(static group => group.IsValid)
               ?? Groups[0];
    }

    private string? ResolveGroupCulture(MarkdownDocumentGroup? group, string? requestedCulture)
    {
        if (group is not { IsMultilingual: true })
        {
            return SelectedCulture;
        }

        return group.ResolvePreferredLanguage(
            requestedCulture,
            SelectedCulture,
            CultureInfo.CurrentUICulture.Name,
            _localizationOption.DefaultCulture);
    }

    private string? ResolveGlobalCulturePreference(string? requestedCulture)
    {
        var supportedCultures = _localizationOption.SupportedCultures
            .Where(static culture => !string.IsNullOrWhiteSpace(culture))
            .Select(static culture => culture.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (supportedCultures.Length == 0)
        {
            return null;
        }

        return TryResolveSupportedCulture(supportedCultures, requestedCulture)
               ?? TryResolveSupportedCulture(supportedCultures, SelectedCulture)
               ?? TryResolveSupportedCulture(supportedCultures, CultureInfo.CurrentUICulture.Name)
               ?? TryResolveSupportedCulture(supportedCultures, _localizationOption.DefaultCulture)
               ?? supportedCultures[0];
    }

    private static string? TryResolveSupportedCulture(
        IReadOnlyList<string> supportedCultures,
        string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        return supportedCultures.FirstOrDefault(supportedCulture =>
            string.Equals(supportedCulture, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private void SetMissingTranslation(string documentRelativePath, string targetCulture)
    {
        MissingTranslation = new MarkdownMissingTranslationState(documentRelativePath, targetCulture);
    }

    private void ClearMissingTranslation()
    {
        MissingTranslation = null;
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
        if (IsDisposeRequested || _interopSession is null)
        {
            return;
        }

        _pendingHeadingTrackerRefresh = false;
        var headingIds = SelectedDocument is null
            ? Array.Empty<string>()
            : DocumentHeadings.Select(static heading => heading.Id).ToArray();

        await _interopSession.RefreshHeadingTrackerAsync(headingIds, CurrentAnchorId);
    }

    private void ResetSearchHighlight()
    {
        _pendingSearchNavigation = null;
        _pendingSearchHighlightClear = true;
    }

    private bool CanApplySearchNavigation(PendingSearchNavigation navigation)
    {
        return !IsDisposeRequested
               && _interopSession is not null
               && !IsContentLoading
               && SelectedDocument is not null
               && DocumentContent is not null
               && string.Equals(SelectedDocument.GroupKey, navigation.Result.GroupKey, StringComparison.Ordinal)
               && string.Equals(
                   SelectedDocument.NavigationRelativePath,
                   navigation.Result.DocumentRelativePath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplySearchHitAsync(PendingSearchNavigation navigation)
    {
        if (IsDisposeRequested || _interopSession is null)
        {
            return;
        }

        _pendingSearchNavigation = null;
        await _interopSession.HighlightSearchHitAsync(navigation.Result.Locator);
    }

    private async Task ClearSearchHitAsync()
    {
        if (IsDisposeRequested || _interopSession is null)
        {
            return;
        }

        _pendingSearchHighlightClear = false;
        await _interopSession.ClearSearchHitAsync();
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
        if (IsDisposeRequested)
        {
            return;
        }

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
        QueueLocation(MarkdownViewerLocation.FromAbsoluteUri(e.Location));
    }

    private void QueueLocation(MarkdownViewerLocation location)
    {
        if (IsDisposeRequested)
        {
            return;
        }

        lock (_locationOperationsLock)
        {
            if (IsDisposeRequested)
            {
                return;
            }

            _activeLocationCancellation?.Cancel();
            var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            _activeLocationCancellation = operationCancellation;

            _locationOperations.RemoveAll(static task => task.IsCompleted);
            _locationOperations.Add(ProcessLocationAsync(location, operationCancellation));
        }
    }

    private async Task ProcessLocationAsync(
        MarkdownViewerLocation location,
        CancellationTokenSource operationCancellation)
    {
        try
        {
            await _groupsReady.Task.WaitAsync(operationCancellation.Token);
            await ApplyLocationAsync(location, operationCancellation.Token);
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsDisposeRequested)
            {
                snackbar.Add(localizer["MarkdownViewer:Messages:LoadDataFailed", ex.Message], Severity.Error);
            }
        }
        finally
        {
            lock (_locationOperationsLock)
            {
                if (ReferenceEquals(_activeLocationCancellation, operationCancellation))
                {
                    _activeLocationCancellation = null;
                }
            }

            operationCancellation.Dispose();
        }
    }

    private Task NotifyStateChangedAsync()
    {
        return IsDisposeRequested
            ? Task.CompletedTask
            : _renderRequestedAsync?.Invoke() ?? Task.CompletedTask;
    }

    private bool IsDisposeRequested => Volatile.Read(ref _disposeRequested) != 0;

    private sealed record PendingSearchNavigation(MarkdownDocumentSearchResult Result);
}
