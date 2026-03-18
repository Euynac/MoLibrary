# UI Page State Pattern

This reference explains how to implement `State/` classes for UI pages in Monica modules.

## Core Concept

A page state class extracts all mutable fields, computed properties, and state-transition logic out of the Razor page `@code` block. The page becomes a thin composition shell that injects the state and wires up events.

## When to Use

- Page has > 10 private fields
- Page owns `CancellationTokenSource`, polling loops, or `Interlocked`
- Page has multiple `Can*()` guard methods
- Multiple components on the same page need to share state

## Registration

Page state classes are registered as `Scoped` services (one instance per circuit/connection in Blazor Server):

```csharp
// In Module registration
services.AddScoped<RAGManagePageState>();
```

Use `Scoped` (not `Transient`) so that the page and all its child components share the same state instance within a single user session.

## Pattern 1: Simple Page State (Data Bag + Computed Properties)

For pages where state is mostly data with some computed properties. No async initialization needed.

```csharp
// UIRAG/State/RAGManagePageState.cs
public sealed class RAGManagePageState : IDisposable
{
    // --- Selection ---
    public KnowledgeBase? SelectedKB { get; private set; }
    public string CurrentEmbeddingModelKey { get; set; } = string.Empty;

    // --- Collections ---
    public List<KnowledgeBase> KnowledgeBases { get; set; } = [];
    public List<EmbeddingModelOption> AvailableModels { get; set; } = [];
    public List<DocumentQueueItem> DocumentQueue { get; set; } = [];
    public List<string> MarkdownGroups { get; set; } = [];

    // --- Loading ---
    public bool IsInitialLoading { get; set; } = true;
    public bool IsSelectionLoading { get; set; }
    public string LoadingPhaseText { get; set; } = string.Empty;
    public double LoadingProgressValue { get; set; } = 12;

    // --- Computed properties replace Can*() guard methods ---
    public bool HasActiveQueueWork =>
        IsBatchIndexingActive
        || DocumentQueue.Any(d => d.Status == DocumentStatus.Indexing)
        || HasSingleDocumentIndexInFlight;

    public bool CanStartBatchIndexing =>
        SelectedKB is not null
        && DocumentQueue.Any(d => d.Status == DocumentStatus.Pending)
        && !HasActiveQueueWork;

    public bool CanCancelBatchIndexing =>
        SelectedKB is not null && IsBatchIndexingActive;

    // --- Internal tracking ---
    public bool IsBatchIndexingActive { get; set; }
    public bool HasSingleDocumentIndexInFlight { get; set; }

    // --- Selection logic ---
    public void SelectKnowledgeBase(KnowledgeBase? kb)
    {
        SelectedKB = kb;
        if (kb is null)
        {
            CurrentEmbeddingModelKey = string.Empty;
            DocumentQueue.Clear();
        }
    }

    public void UpdateLoadingState(string phaseText, double progressValue)
    {
        LoadingPhaseText = phaseText;
        LoadingProgressValue = progressValue;
    }

    public void Dispose()
    {
        // Clean up any resources
    }
}
```

## Pattern 2: State with Async Operations (Facade-Calling State)

When the state class needs to call Facades for loading data. Inject Facades directly — no intermediate UIService wrapper.

```csharp
// UIRAG/State/RAGManagePageState.cs
public sealed class RAGManagePageState(
    RAGFacade ragFacade,
    EmbeddingModelFacade embeddingFacade
) : IDisposable
{
    // --- State fields (same as Pattern 1) ---
    public KnowledgeBase? SelectedKB { get; private set; }
    public List<KnowledgeBase> KnowledgeBases { get; private set; } = [];
    public List<DocumentQueueItem> DocumentQueue { get; private set; } = [];
    public bool IsInitialLoading { get; private set; } = true;

    // --- Event for notifying the page to re-render ---
    public event Action? StateChanged;

    private void NotifyStateChanged() => StateChanged?.Invoke();

    // --- Async initialization ---
    public async Task InitializeAsync()
    {
        IsInitialLoading = true;
        NotifyStateChanged();

        var result = await ragFacade.GetKnowledgeBasesAsync();
        if (!result.IsFailed(out _, out var kbs))
        {
            KnowledgeBases = kbs.ToList();
        }

        IsInitialLoading = false;
        NotifyStateChanged();
    }

    // --- Selection with data loading ---
    public async Task SelectKnowledgeBaseAsync(KnowledgeBase? kb)
    {
        SelectedKB = kb;
        if (kb is null)
        {
            DocumentQueue.Clear();
            NotifyStateChanged();
            return;
        }

        var result = await ragFacade.GetDocumentQueueAsync(kb.Id);
        if (!result.IsFailed(out _, out var queue))
        {
            DocumentQueue = queue.ToList();
        }

        NotifyStateChanged();
    }

    public void Dispose()
    {
        // Clean up subscriptions, CTS, etc.
    }
}
```

Page wiring:

```razor
@inject RAGManagePageState PageState
@implements IDisposable

<KnowledgeBaseListPanel KnowledgeBases="@PageState.KnowledgeBases"
                        SelectedKB="@PageState.SelectedKB"
                        OnSelected="@PageState.SelectKnowledgeBaseAsync" />

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        PageState.StateChanged += OnStateChanged;
        await PageState.InitializeAsync();
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        PageState.StateChanged -= OnStateChanged;
        PageState.Dispose();
    }
}
```

## Pattern 3: Separate Polling/Concurrency State

When a page has polling loops or concurrency primitives, extract them into a dedicated state class. Keep the main page state clean.

```csharp
// UIRAG/State/RAGQueuePollingState.cs
public sealed class RAGQueuePollingState(RAGFacade ragFacade) : IDisposable
{
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private int _refreshInFlight;

    public bool IsPolling => _pollingTask is { IsCompleted: false };

    /// <summary>
    /// Notify consumers that queue data has been refreshed.
    /// </summary>
    public event Action<List<DocumentQueueItem>>? QueueRefreshed;

    public void EnsurePolling(string knowledgeBaseId, Func<bool> hasActiveWork)
    {
        if (!hasActiveWork())
        {
            Stop();
            return;
        }

        if (IsPolling) return;

        _pollingCts = new CancellationTokenSource();
        _pollingTask = RunPollingLoopAsync(knowledgeBaseId, hasActiveWork, _pollingCts.Token);
    }

    public void Stop()
    {
        if (_pollingCts is null) return;

        var cts = _pollingCts;
        _pollingCts = null;
        _pollingTask = null;

        if (!cts.IsCancellationRequested) cts.Cancel();
        cts.Dispose();
    }

    private async Task RunPollingLoopAsync(
        string knowledgeBaseId,
        Func<bool> hasActiveWork,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (Interlocked.Exchange(ref _refreshInFlight, 1) == 0)
                {
                    try
                    {
                        var result = await ragFacade.GetDocumentQueueAsync(knowledgeBaseId);
                        if (!result.IsFailed(out _, out var queue))
                        {
                            QueueRefreshed?.Invoke(queue.ToList());
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _refreshInFlight, 0);
                    }
                }

                if (!hasActiveWork()) break;
                await Task.Delay(700, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => Stop();
}
```

Main state consumes polling state:

```csharp
// In RAGManagePageState constructor
public sealed class RAGManagePageState(
    RAGFacade ragFacade,
    RAGQueuePollingState pollingState
) : IDisposable
{
    // Subscribe to polling updates
    public void Initialize()
    {
        pollingState.QueueRefreshed += OnQueueRefreshed;
    }

    private void OnQueueRefreshed(List<DocumentQueueItem> queue)
    {
        DocumentQueue = queue;
        NotifyStateChanged();
    }

    public void Dispose()
    {
        pollingState.QueueRefreshed -= OnQueueRefreshed;
        pollingState.Dispose();
    }
}
```

## Choosing the Right Pattern

| Scenario | Pattern |
|----------|---------|
| Simple data + computed properties | Pattern 1 |
| State needs to call Facades | Pattern 2 |
| Polling, CTS, Interlocked | Pattern 3 |
| Complex page with all of the above | Pattern 2 + 3 combined |

## Rules

- State classes go in `UI{Name}/State/`
- Register as `Scoped` (shared across page + child components in one circuit)
- Inject Facades directly — no intermediate `*UIService` wrappers
- Use `event Action? StateChanged` to notify the page to re-render
- Page subscribes in `OnAfterRenderAsync(firstRender)`, unsubscribes in `Dispose()`
- Keep `Can*()` logic as computed properties on the state class, not methods on the page
- Separate polling/concurrency into its own state class when present
- State classes must implement `IDisposable` to clean up CTS and event subscriptions
