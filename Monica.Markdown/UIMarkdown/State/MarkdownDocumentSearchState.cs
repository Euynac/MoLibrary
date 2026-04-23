using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Markdown.Facades;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Models;
using Monica.Modules;

namespace Monica.Markdown.UIMarkdown.State;

/// <summary>
/// Owns the transient state for the markdown document search dialog.
/// </summary>
public sealed class MarkdownDocumentSearchState(
    MarkdownFacade markdownFacade,
    IOptions<ModuleMarkdownOption> markdownOptions,
    IOptions<ModuleMarkdownUIOption> markdownUiOptions)
    : IDisposable
{
    private readonly ModuleMarkdownOption _markdownOption = markdownOptions.Value;
    private readonly ModuleMarkdownUIOption _markdownUiOption = markdownUiOptions.Value;
    private CancellationTokenSource? _searchCts;
    private Func<Task>? _notifyChangedAsync;

    /// <summary>
    /// The knowledge base currently selected when the dialog opens.
    /// </summary>
    public MarkdownDocumentGroup? CurrentGroup { get; private set; }

    /// <summary>
    /// The active document culture when the dialog opens.
    /// </summary>
    public string? CurrentCulture { get; private set; }

    /// <summary>
    /// The current raw query text.
    /// </summary>
    public string Query { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the search should include every knowledge base.
    /// </summary>
    public bool IncludeAllKnowledgeBases { get; private set; }

    /// <summary>
    /// Whether a search request is currently in flight.
    /// </summary>
    public bool IsSearching { get; private set; }

    /// <summary>
    /// The latest search error, if one occurred.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// The latest search results.
    /// </summary>
    public IReadOnlyList<MarkdownDocumentSearchResult> Results { get; private set; } = [];

    /// <summary>
    /// Minimum query length required before search execution.
    /// </summary>
    public int MinimumQueryLength => Math.Max(1, _markdownOption.DocumentSearchMinQueryLength);

    /// <summary>
    /// Debounce interval used by the dialog input.
    /// </summary>
    public double SearchDebounceMilliseconds =>
        Math.Max(0, _markdownUiOption.DocumentSearchDebounceMilliseconds);

    /// <summary>
    /// Whether the dialog currently has a non-empty query.
    /// </summary>
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(Query);

    /// <summary>
    /// Whether the current query satisfies the minimum search length.
    /// </summary>
    public bool HasEnoughQueryLength => Query.Trim().Length >= MinimumQueryLength;

    /// <summary>
    /// Attaches the current dialog context and UI refresh callback.
    /// </summary>
    public void Attach(
        MarkdownDocumentGroup? currentGroup,
        string? currentCulture,
        Func<Task> notifyChangedAsync)
    {
        CurrentGroup = currentGroup;
        CurrentCulture = currentCulture;
        _notifyChangedAsync = notifyChangedAsync;
    }

    /// <summary>
    /// Updates the query from the input field.
    /// </summary>
    public void OnQueryChanged(string? value)
    {
        Query = value ?? string.Empty;

        if (HasEnoughQueryLength)
        {
            return;
        }

        CancelSearch();
        ErrorMessage = null;
        Results = [];
        IsSearching = false;
        _ = NotifyChangedAsync();
    }

    /// <summary>
    /// Executes a debounced search when the input settles.
    /// </summary>
    public async Task OnQueryDebouncedAsync(string value)
    {
        Query = value ?? string.Empty;
        await ExecuteSearchAsync();
    }

    /// <summary>
    /// Toggles whether all knowledge bases participate in the search.
    /// </summary>
    public async Task OnIncludeAllKnowledgeBasesChangedAsync(bool value)
    {
        IncludeAllKnowledgeBases = value;
        await ExecuteSearchAsync();
    }

    /// <summary>
    /// Cancels any in-flight search and releases resources.
    /// </summary>
    public void Dispose()
    {
        CancelSearch();
    }

    private async Task ExecuteSearchAsync()
    {
        CancelSearch();
        ErrorMessage = null;

        if (!HasEnoughQueryLength)
        {
            Results = [];
            IsSearching = false;
            await NotifyChangedAsync();
            return;
        }

        var searchCts = new CancellationTokenSource();
        _searchCts = searchCts;
        IsSearching = true;
        await NotifyChangedAsync();

        try
        {
            var response = await markdownFacade.SearchDocumentsAsync(
                new MarkdownDocumentSearchRequest(
                    Query.Trim(),
                    CurrentGroup?.Key,
                    IncludeAllKnowledgeBases,
                    CurrentCulture),
                searchCts.Token);

            if (searchCts.IsCancellationRequested)
            {
                return;
            }

            if (response.IsFailed(out var error, out var results))
            {
                ErrorMessage = error;
                Results = [];
                return;
            }

            Results = results;
        }
        catch (OperationCanceledException) when (searchCts.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_searchCts, searchCts))
            {
                IsSearching = false;
                _searchCts = null;
            }

            searchCts.Dispose();
            await NotifyChangedAsync();
        }
    }

    private void CancelSearch()
    {
        if (_searchCts is null)
        {
            return;
        }

        _searchCts.Cancel();
        _searchCts.Dispose();
        _searchCts = null;
    }

    private Task NotifyChangedAsync()
    {
        return _notifyChangedAsync?.Invoke() ?? Task.CompletedTask;
    }
}
