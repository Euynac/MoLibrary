using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Monica.AI.Abstractions;
using Monica.AI.RAG.Facades;
using Monica.AI.RAG.Models;
using Monica.AI.UI.Localization;
using Monica.AI.UI.UIRAG.Components;
using Monica.AI.UI.UIRAG.Models;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIRAG.State;

/// <summary>
/// Owns mutable page state and UI orchestration for the RAG management page.
/// </summary>
public sealed class RAGManagePageState(
    RAGFacade ragFacade,
    EmbeddingModelFacade embeddingFacade,
    IAIProviderFactory providerFactory,
    ISnackbar snackbar,
    IDialogService dialogService,
    IStringLocalizer<AIResource> localizer,
    RAGQueuePollingState queuePollingState) : IDisposable
{
    private bool _isAttached;
    private bool _embeddingModelWarningShown;
    private string? _batchStartInFlightKnowledgeBaseId;
    private string? _singleIndexInFlightKnowledgeBaseId;
    private string? _singleIndexInFlightDocumentId;

    /// <summary>
    /// Raised when the page should re-render.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// All knowledge bases shown in the sidebar.
    /// </summary>
    public IReadOnlyList<KnowledgeBase> KnowledgeBases { get; private set; } = [];

    /// <summary>
    /// Current knowledge base selection.
    /// </summary>
    public KnowledgeBase? SelectedKnowledgeBase { get; private set; }

    /// <summary>
    /// Current embedding model key selected in the page.
    /// </summary>
    public string CurrentEmbeddingModelKey { get; private set; } = string.Empty;

    /// <summary>
    /// Available embedding model options.
    /// </summary>
    public IReadOnlyList<EmbeddingModelOption> AvailableModels { get; private set; } = [];

    /// <summary>
    /// Current document queue for the selected knowledge base.
    /// </summary>
    public IReadOnlyList<DocumentQueueItem> DocumentQueue { get; private set; } = [];

    /// <summary>
    /// Maximum parallel indexing concurrency requested by the UI.
    /// </summary>
    public int ParallelCount { get; set; } = 5;

    /// <summary>
    /// Markdown groups available for queue import.
    /// </summary>
    public IReadOnlyList<string> MarkdownGroups { get; private set; } = [];

    /// <summary>
    /// Markdown group currently selected in the page.
    /// </summary>
    public string? SelectedMarkdownGroup { get; set; }

    /// <summary>
    /// Vector validation result for the current selection.
    /// </summary>
    public KnowledgeBaseVectorValidationResult? SelectedKnowledgeBaseVectorValidation { get; private set; }

    /// <summary>
    /// Whether the first page load is still running.
    /// </summary>
    public bool IsInitialLoading { get; private set; } = true;

    /// <summary>
    /// Whether the current knowledge-base selection is still loading.
    /// </summary>
    public bool IsSelectionLoading { get; private set; }

    /// <summary>
    /// Whether embedding-model metadata was loaded once already.
    /// </summary>
    public bool AreEmbeddingModelsLoaded { get; private set; }

    /// <summary>
    /// Whether markdown-group metadata was loaded once already.
    /// </summary>
    public bool AreMarkdownGroupsLoaded { get; private set; }

    /// <summary>
    /// Whether markdown groups are currently warming up.
    /// </summary>
    public bool IsMarkdownGroupsLoading { get; private set; }

    /// <summary>
    /// Current loading-phase label shown by the page.
    /// </summary>
    public string LoadingPhaseText { get; private set; } = string.Empty;

    /// <summary>
    /// Current loading progress shown by the page.
    /// </summary>
    public double LoadingProgressValue { get; private set; } = 12;

    /// <summary>
    /// Whether queue activity currently exists for the selected knowledge base.
    /// </summary>
    public bool HasActiveQueueWork
        => IsBatchIndexingRunning
           || HasIndexingRowsInFlight
           || HasSingleDocumentIndexInFlight;

    /// <summary>
    /// Whether batch indexing can start for the current selection.
    /// </summary>
    public bool CanStartBatchIndexing
        => SelectedKnowledgeBase is not null
           && DocumentQueue.Any(document => document.Status == DocumentStatus.Pending)
           && !HasActiveQueueWork;

    /// <summary>
    /// Whether batch indexing can be cancelled for the current selection.
    /// </summary>
    public bool CanCancelBatchIndexing
        => SelectedKnowledgeBase is not null
           && IsBatchIndexingRunning;

    /// <summary>
    /// Whether all queued documents can be removed for the current selection.
    /// </summary>
    public bool CanClearKnowledgeBaseDocuments
        => SelectedKnowledgeBase is not null
           && DocumentQueue.Count > 0
           && !HasActiveQueueWork;

    /// <summary>
    /// Whether pending/error queue rows can be cleared for the current selection.
    /// </summary>
    public bool CanClearDocumentQueue
        => SelectedKnowledgeBase is not null
           && DocumentQueue.Any(document => document.Status != DocumentStatus.Done)
           && !HasActiveQueueWork;

    /// <summary>
    /// Whether missing vectors can be repaired through reindexing.
    /// </summary>
    public bool CanReindexSelectedKnowledgeBase
        => SelectedKnowledgeBase is not null
           && ShouldShowMissingVectorWarning
           && !HasActiveQueueWork;

    /// <summary>
    /// Whether the current vector-validation result indicates missing vectors.
    /// </summary>
    public bool ShouldShowMissingVectorWarning
        => SelectedKnowledgeBaseVectorValidation is
        {
            HasPersistedIndexMetadata: true,
            WasValidated: true,
            HasValidVectors: false
        };

    private bool IsBatchIndexingRunning
    {
        get
        {
            if (SelectedKnowledgeBase is null)
            {
                return false;
            }

            if (string.Equals(
                    _batchStartInFlightKnowledgeBaseId,
                    SelectedKnowledgeBase.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ragFacade.IsBatchIndexingActive(SelectedKnowledgeBase.Id);
        }
    }

    private bool HasIndexingRowsInFlight
        => DocumentQueue.Any(document => document.Status == DocumentStatus.Indexing);

    private bool HasSingleDocumentIndexInFlight
        => SelectedKnowledgeBase is not null
           && !string.IsNullOrWhiteSpace(_singleIndexInFlightDocumentId)
           && string.Equals(
               _singleIndexInFlightKnowledgeBaseId,
               SelectedKnowledgeBase.Id,
               StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Attach the state to background polling notifications.
    /// </summary>
    public void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        queuePollingState.QueueRefreshed += OnQueueRefreshed;
        queuePollingState.QueueRefreshFailed += OnQueueRefreshFailed;
        _isAttached = true;
    }

    /// <summary>
    /// Initialize the page for the current visit.
    /// </summary>
    public async Task InitializeAsync()
    {
        Attach();

        IsInitialLoading = true;
        UpdateLoadingState(localizer["RAG:KnowledgeBase:Title"].Value, 35);
        await LoadKnowledgeBasesAsync();
        LoadingProgressValue = 100;
        IsInitialLoading = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Change the current knowledge-base selection and load dependent data.
    /// </summary>
    public async Task SelectKnowledgeBaseAsync(KnowledgeBase? knowledgeBase)
    {
        if (!string.Equals(
                _batchStartInFlightKnowledgeBaseId,
                knowledgeBase?.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            ClearBatchStartInFlight();
        }

        if (!string.Equals(
                _singleIndexInFlightKnowledgeBaseId,
                knowledgeBase?.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            ClearSingleIndexInFlight();
        }

        SelectedKnowledgeBase = knowledgeBase;
        SelectedKnowledgeBaseVectorValidation = null;

        if (knowledgeBase is null)
        {
            CurrentEmbeddingModelKey = string.Empty;
            DocumentQueue = [];
            SelectedKnowledgeBaseVectorValidation = null;
            ClearBatchStartInFlight();
            ClearSingleIndexInFlight();
            queuePollingState.Stop();
            NotifyStateChanged();
            return;
        }

        IsSelectionLoading = true;
        UpdateLoadingState(localizer["RAG:EmbeddingModel:Label"].Value, 28);
        NotifyStateChanged();

        if (!AreEmbeddingModelsLoaded)
        {
            await LoadEmbeddingModelsAsync();
        }

        CurrentEmbeddingModelKey = HasEmbeddingBinding(knowledgeBase)
            ? GetKnowledgeBaseModelKey(knowledgeBase)
            : string.Empty;

        UpdateLoadingState(localizer["RAG:DocumentQueue:Title"].Value, 72);
        await LoadDocumentQueueAsync();
        EnsureQueuePolling();
        IsSelectionLoading = false;
        LoadingProgressValue = 100;
        NotifyStateChanged();
        QueueMarkdownGroupWarmup();
        QueueSelectedKnowledgeBaseVectorValidation(knowledgeBase.Id);
    }

    /// <summary>
    /// Delete the current knowledge-base selection if present.
    /// </summary>
    public async Task DeleteSelectedKnowledgeBaseAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        await DeleteKnowledgeBaseAsync(SelectedKnowledgeBase);
    }

    /// <summary>
    /// Delete one knowledge base after confirmation.
    /// </summary>
    public async Task DeleteKnowledgeBaseAsync(KnowledgeBase knowledgeBase)
    {
        var confirmed = await dialogService.ShowMessageBoxAsync(
            localizer["Common:Confirm"],
            $"Are you sure you want to delete knowledge base '{knowledgeBase.Name}'? This will remove all indexed documents.",
            yesText: localizer["Common:Delete"],
            cancelText: localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        if ((await ragFacade.DeleteKnowledgeBaseAsync(knowledgeBase.Id)).IsFailed(out var error))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["Common:Success"], Severity.Success);
        if (string.Equals(SelectedKnowledgeBase?.Id, knowledgeBase.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedKnowledgeBase = null;
            SelectedKnowledgeBaseVectorValidation = null;
            CurrentEmbeddingModelKey = string.Empty;
            DocumentQueue = [];
            ClearBatchStartInFlight();
            ClearSingleIndexInFlight();
            queuePollingState.Stop();
        }

        await LoadKnowledgeBasesAsync();
        NotifyStateChanged();
    }

    /// <summary>
    /// Change the embedding binding for the current knowledge base.
    /// </summary>
    public async Task ChangeEmbeddingModelAsync(string modelKey)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if (!EmbeddingModelOption.TryParseModelKey(modelKey, out var providerId, out var modelName))
        {
            snackbar.Add($"{localizer["Common:Error"]}: invalid embedding model key.", Severity.Error);
            return;
        }

        var previousModelKey = GetKnowledgeBaseModelKey(SelectedKnowledgeBase);
        if (string.Equals(previousModelKey, modelKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var requiresReindexWarning = HasIndexedContent(SelectedKnowledgeBase)
            && !string.IsNullOrWhiteSpace(previousModelKey);

        if (requiresReindexWarning)
        {
            var confirmed = await dialogService.ShowMessageBoxAsync(
                localizer["RAG:EmbeddingModel:ReindexConfirm:Title"],
                localizer["RAG:EmbeddingModel:ReindexConfirm:Message", SelectedKnowledgeBase.Name],
                yesText: localizer["RAG:EmbeddingModel:ReindexConfirm:Continue"],
                cancelText: localizer["Common:Cancel"]);

            if (confirmed != true)
            {
                CurrentEmbeddingModelKey = previousModelKey;
                NotifyStateChanged();
                return;
            }
        }

        CurrentEmbeddingModelKey = modelKey;
        NotifyStateChanged();

        if ((await embeddingFacade.SetKnowledgeBaseEmbeddingModelAsync(
                SelectedKnowledgeBase.Id,
                providerId,
                modelName,
                clearIndex: true)).IsFailed(out var error))
        {
            CurrentEmbeddingModelKey = previousModelKey;
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            NotifyStateChanged();
            return;
        }

        await RefreshSelectedKnowledgeBaseAsync();

        if (SelectedKnowledgeBase is not null)
        {
            await LoadDocumentQueueAsync();
        }

        snackbar.Add(
            requiresReindexWarning
                ? localizer["RAG:EmbeddingModel:ReindexRequired"]
                : localizer["Common:Success"],
            requiresReindexWarning ? Severity.Warning : Severity.Success);
    }

    /// <summary>
    /// Open the create knowledge-base dialog.
    /// </summary>
    public async Task ShowCreateKnowledgeBaseDialogAsync()
    {
        var dialog = await dialogService.ShowAsync<CreateKnowledgeBaseDialog>(
            localizer["RAG:Manage:CreateDialog:Title"],
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: KnowledgeBaseDialogResult data })
        {
            return;
        }

        if ((await ragFacade.CreateKnowledgeBaseAsync(data.Id, data.Name, data.Description)).IsFailed(out var error))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["Common:Success"], Severity.Success);
        await LoadKnowledgeBasesAsync();
    }

    /// <summary>
    /// Open the edit knowledge-base dialog for the current selection.
    /// </summary>
    public async Task ShowEditKnowledgeBaseDialogAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var parameters = new DialogParameters
        {
            { nameof(CreateKnowledgeBaseDialog.InitialId), SelectedKnowledgeBase.Id },
            { nameof(CreateKnowledgeBaseDialog.InitialName), SelectedKnowledgeBase.Name },
            { nameof(CreateKnowledgeBaseDialog.InitialDescription), SelectedKnowledgeBase.Description },
            { nameof(CreateKnowledgeBaseDialog.SubmitText), localizer["Common:Actions:Save"].Value },
            { nameof(CreateKnowledgeBaseDialog.IsIdReadOnly), true }
        };

        var dialog = await dialogService.ShowAsync<CreateKnowledgeBaseDialog>(
            localizer["RAG:Manage:EditDialog:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: KnowledgeBaseDialogResult data })
        {
            return;
        }

        if ((await ragFacade.UpdateKnowledgeBaseAsync(
                SelectedKnowledgeBase.Id,
                data.Name,
                data.Description)).IsFailed(out var error))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["Common:Success"], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
    }

    /// <summary>
    /// Start batch indexing for all pending queue rows.
    /// </summary>
    public async Task StartBatchIndexingAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if (!EnsureEmbeddingModelConfiguredForIndexing())
        {
            return;
        }

        var selectedKnowledgeBaseId = SelectedKnowledgeBase.Id;
        var pendingDocuments = DocumentQueue.Where(document => document.Status == DocumentStatus.Pending).ToList();
        if (pendingDocuments.Count == 0)
        {
            snackbar.Add(localizer["RAG:ParallelIndexing:NoPendingDocuments"], Severity.Warning);
            return;
        }

        _batchStartInFlightKnowledgeBaseId = selectedKnowledgeBaseId;
        EnsureQueuePolling();
        NotifyStateChanged();

        var progress = new Progress<IndexingProgress>(OnBatchIndexingProgress);
        try
        {
            var result = await ragFacade.StartBatchIndexingAsync(
                selectedKnowledgeBaseId,
                ParallelCount,
                progress);

            if (result.IsFailed(out var error))
            {
                if (IsCancellationMessage(error.Message))
                {
                    snackbar.Add(localizer["Error:RequestCancelled"], Severity.Warning);
                }
                else
                {
                    snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
                    await dialogService.ShowMessageBoxAsync(
                        localizer["Common:Error"],
                        error.Message ?? localizer["Common:Error"].Value,
                        yesText: localizer["Common:Close"]);
                }
            }
            else
            {
                snackbar.Add(localizer["Common:Success"], Severity.Success);
            }
        }
        finally
        {
            if (string.Equals(
                    _batchStartInFlightKnowledgeBaseId,
                    selectedKnowledgeBaseId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _batchStartInFlightKnowledgeBaseId = null;
            }

            await RefreshSelectedKnowledgeBaseAsync();
            await LoadDocumentQueueAsync(ensurePolling: false);
            EnsureQueuePolling();
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Start indexing for one queue row.
    /// </summary>
    public async Task StartDocumentIndexingAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if (!EnsureEmbeddingModelConfiguredForIndexing())
        {
            return;
        }

        var selectedKnowledgeBaseId = SelectedKnowledgeBase.Id;
        _singleIndexInFlightKnowledgeBaseId = selectedKnowledgeBaseId;
        _singleIndexInFlightDocumentId = document.Id;
        EnsureQueuePolling();
        NotifyStateChanged();

        try
        {
            var progress = new Progress<IndexingProgress>(OnBatchIndexingProgress);
            var result = await ragFacade.StartDocumentIndexingAsync(
                selectedKnowledgeBaseId,
                document.Id,
                progress);
            if (result.IsFailed(out var error))
            {
                snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
                return;
            }

            snackbar.Add(localizer["Common:Success"], Severity.Success);
        }
        finally
        {
            ClearSingleIndexInFlight();
            await RefreshSelectedKnowledgeBaseAsync();
            await LoadDocumentQueueAsync(ensurePolling: false);
            EnsureQueuePolling();
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Cancel batch indexing for the current knowledge base.
    /// </summary>
    public async Task CancelBatchIndexingAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var cancelResult = await ragFacade.CancelBatchIndexingAsync(SelectedKnowledgeBase.Id);
        if (cancelResult.IsFailed(out var error))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        ClearBatchStartInFlight();
        EnsureQueuePolling();
        snackbar.Add(cancelResult.Message ?? localizer["Error:RequestCancelled"].Value, Severity.Warning);
        await LoadDocumentQueueAsync();
        await RefreshSelectedKnowledgeBaseAsync();
        NotifyStateChanged();
    }

    /// <summary>
    /// Remove all queue rows and indexed documents for the current selection.
    /// </summary>
    public async Task ClearKnowledgeBaseDocumentsAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var confirmed = await dialogService.ShowMessageBoxAsync(
            localizer["Common:Confirm"],
            localizer["RAG:DocumentQueue:ClearDocumentsConfirm", SelectedKnowledgeBase.Name],
            yesText: localizer["RAG:DocumentQueue:Actions:ClearDocuments"],
            cancelText: localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        var result = await ragFacade.ClearKnowledgeBaseDocumentsAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var removedCount))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["RAG:DocumentQueue:Messages:DocumentsCleared", removedCount], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Clear pending and failed queue rows for the current selection.
    /// </summary>
    public async Task ClearDocumentQueueAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var confirmed = await dialogService.ShowMessageBoxAsync(
            localizer["Common:Confirm"],
            localizer["RAG:DocumentQueue:ClearQueueConfirm", SelectedKnowledgeBase.Name],
            yesText: localizer["RAG:DocumentQueue:Actions:ClearQueue"],
            cancelText: localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        var result = await ragFacade.ClearDocumentQueueAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var removedCount))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["RAG:DocumentQueue:Messages:QueueCleared", removedCount], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Open the chunk viewer dialog for one queue row.
    /// </summary>
    public async Task ShowChunkViewerAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var result = await ragFacade.GetDocumentChunksAsync(SelectedKnowledgeBase.Id, document.Id);
        if (result.IsFailed(out var error, out var view))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        var statusText = document.Status switch
        {
            DocumentStatus.Done => localizer["RAG:ChunkViewer:Indexed", document.IndexedAt?.ToString("yyyy-MM-dd") ?? ""].Value,
            DocumentStatus.Pending => localizer["RAG:ChunkViewer:PendingIndexing"].Value,
            _ => document.Status.ToString()
        };

        var parameters = new DialogParameters
        {
            { nameof(ChunkViewerDialog.DocumentName), document.Name },
            { nameof(ChunkViewerDialog.DocumentPath), view.DocumentPath },
            { nameof(ChunkViewerDialog.OriginalText), view.OriginalText },
            { nameof(ChunkViewerDialog.Chunks), view.Chunks },
            { nameof(ChunkViewerDialog.MatchedChunkIndex), (int?)null },
            { nameof(ChunkViewerDialog.StatusText), statusText },
            { nameof(ChunkViewerDialog.SourceKind), view.SourceKind },
            { nameof(ChunkViewerDialog.SourceGroupKey), view.SourceGroupKey }
        };

        await dialogService.ShowAsync<ChunkViewerDialog>(
            localizer["RAG:ChunkViewer:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true });
    }

    /// <summary>
    /// Queue one document for reindexing.
    /// </summary>
    public async Task ReindexDocumentAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if ((await ragFacade.ReindexDocumentAsync(SelectedKnowledgeBase.Id, document.Id)).IsFailed(out var error))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["Common:Success"], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Delete one queue document after confirmation.
    /// </summary>
    public async Task DeleteDocumentAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var confirmed = await dialogService.ShowMessageBoxAsync(
            localizer["Common:Confirm"],
            localizer["RAG:DocumentQueue:DeleteConfirm", document.Name],
            yesText: localizer["Common:Delete"],
            cancelText: localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        if ((await ragFacade.RemoveDocumentAsync(SelectedKnowledgeBase.Id, document.Id)).IsFailed(out var error))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["Common:Success"], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Queue all indexed documents in the selected knowledge base for reindexing.
    /// </summary>
    public async Task ReindexSelectedKnowledgeBaseAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var result = await ragFacade.ReindexKnowledgeBaseAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var queuedCount))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["RAG:VectorValidation:Messages:ReindexQueued", queuedCount], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Show the full indexing error for one queue row.
    /// </summary>
    public async Task ShowIndexingErrorAsync(DocumentQueueItem document)
    {
        var message = string.IsNullOrWhiteSpace(document.ErrorMessage)
            ? localizer["Common:Error"].Value
            : document.ErrorMessage;

        await dialogService.ShowMessageBoxAsync(
            localizer["Common:Error"],
            $"{document.Name}\n\n{message}",
            yesText: localizer["Common:Close"]);
    }

    /// <summary>
    /// Open the markdown document-selection dialog for the current selection.
    /// </summary>
    public async Task ShowDocumentSelectionDialogAsync()
    {
        if (SelectedKnowledgeBase is null || string.IsNullOrEmpty(SelectedMarkdownGroup))
        {
            return;
        }

        if ((await ragFacade.GetAvailableDocumentsAsync(SelectedMarkdownGroup)).IsFailed(out var error, out var documents))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        var indexedDocumentIds = DocumentQueue
            .Where(document => document.Status == DocumentStatus.Done)
            .Select(document => document.Id)
            .ToHashSet();

        var parameters = new DialogParameters
        {
            { nameof(DocumentSelectionDialog.AvailableDocuments), documents.ToList() },
            { nameof(DocumentSelectionDialog.IndexedDocumentIds), indexedDocumentIds }
        };

        var dialog = await dialogService.ShowAsync<DocumentSelectionDialog>(
            localizer["RAG:DocumentSelection:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: List<string> selectedDocumentIds })
        {
            return;
        }

        if ((await ragFacade.AddDocumentsToQueueAsync(
                SelectedKnowledgeBase.Id,
                selectedDocumentIds,
                SelectedMarkdownGroup)).IsFailed(out var addError))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {addError.Message}", Severity.Error);
            return;
        }

        snackbar.Add(localizer["Common:Success"], Severity.Success);
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Upload one file and index it into the selected knowledge base.
    /// </summary>
    public async Task UploadFileAsync(IBrowserFile file)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if (!EnsureEmbeddingModelConfiguredForIndexing())
        {
            return;
        }

        try
        {
            const long maxFileSize = 10 * 1024 * 1024;
            if (file.Size > maxFileSize)
            {
                snackbar.Add(localizer["RAG:Indexing:FileTooLarge"], Severity.Error);
                return;
            }

            using var stream = file.OpenReadStream(maxFileSize);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            if ((await ragFacade.UploadAndIndexDocumentAsync(
                    SelectedKnowledgeBase.Id,
                    file.Name,
                    content)).IsFailed(out var error))
            {
                snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
                return;
            }

            snackbar.Add(localizer["Common:Success"], Severity.Success);
            await RefreshSelectedKnowledgeBaseAsync();
            await LoadDocumentQueueAsync();
        }
        catch (Exception ex)
        {
            snackbar.Add($"{localizer["Common:Error"]}: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// Check whether one knowledge base already has an embedding binding.
    /// </summary>
    public static bool HasEmbeddingBinding(KnowledgeBase knowledgeBase)
        => !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
           && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName);

    /// <summary>
    /// Check whether one queue row can be indexed now.
    /// </summary>
    public bool CanStartDocumentIndexing(DocumentQueueItem document)
        => SelectedKnowledgeBase is not null
           && document.Status is DocumentStatus.Pending or DocumentStatus.Error
           && !HasActiveQueueWork;

    /// <summary>
    /// Resolve the display label of one provider.
    /// </summary>
    public string GetProviderDisplayLabel(string? providerId, string? modelName = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return string.Empty;
        }

        var model = AvailableModels.FirstOrDefault(option =>
            string.Equals(option.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(modelName)
                || string.Equals(option.ModelName, modelName, StringComparison.OrdinalIgnoreCase)));

        if (model is not null)
        {
            return model.ProviderDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var modelCandidates = AvailableModels
                .Where(option => string.Equals(option.ModelName, modelName, StringComparison.OrdinalIgnoreCase))
                .Select(option => option.ProviderDisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (modelCandidates.Count == 1)
            {
                return modelCandidates[0];
            }
        }

        var provider = providerFactory.GetProvider(providerId);
        if (provider is not null)
        {
            return provider.DisplayName;
        }

        return providerId.Trim();
    }

    /// <summary>
    /// Resolve one knowledge-base embedding display string.
    /// </summary>
    public string GetKnowledgeBaseModelDisplay(KnowledgeBase knowledgeBase)
    {
        if (!HasEmbeddingBinding(knowledgeBase))
        {
            return string.Empty;
        }

        return $"{knowledgeBase.EmbeddingModelName} ({GetProviderDisplayLabel(knowledgeBase.EmbeddingProviderId, knowledgeBase.EmbeddingModelName)})";
    }

    /// <summary>
    /// Resolve one model display string by the composite model key.
    /// </summary>
    public string GetModelDisplayByKey(string modelKey)
    {
        var model = AvailableModels.FirstOrDefault(option =>
            string.Equals(option.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase));

        if (model is not null)
        {
            return $"{model.ModelName} ({model.ProviderDisplayName})";
        }

        if (!EmbeddingModelOption.TryParseModelKey(modelKey, out var providerId, out var modelName))
        {
            return modelKey;
        }

        return $"{modelName} ({GetProviderDisplayLabel(providerId, modelName)})";
    }

    /// <summary>
    /// Build the missing-vector warning copy for the current selection.
    /// </summary>
    public string GetMissingVectorWarningMessage()
    {
        if (SelectedKnowledgeBaseVectorValidation is null)
        {
            return string.Empty;
        }

        return localizer["RAG:VectorValidation:MissingVectors:Message",
            SelectedKnowledgeBaseVectorValidation.IndexedDocumentCount,
            SelectedKnowledgeBaseVectorValidation.IndexedChunkCount];
    }

    /// <summary>
    /// Resolve the persisted embedding model key for one knowledge base.
    /// </summary>
    public string GetKnowledgeBaseModelKey(KnowledgeBase knowledgeBase)
    {
        if (!HasEmbeddingBinding(knowledgeBase))
        {
            return string.Empty;
        }

        return EmbeddingModelOption.ToModelKey(
            knowledgeBase.EmbeddingProviderId!,
            knowledgeBase.EmbeddingModelName!);
    }

    private async Task<bool> LoadEmbeddingModelsAsync()
    {
        if ((await embeddingFacade.GetEmbeddingModelsAsync()).IsFailed(out var error, out var models))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return false;
        }

        AvailableModels = models.ToList();
        AreEmbeddingModelsLoaded = true;

        if (AvailableModels.Count == 0 && !_embeddingModelWarningShown)
        {
            _embeddingModelWarningShown = true;
            snackbar.Add(localizer["RAG:EmbeddingModel:NotConfigured"], Severity.Warning);
        }

        NotifyStateChanged();
        return true;
    }

    private async Task<bool> LoadKnowledgeBasesAsync()
    {
        if ((await ragFacade.GetKnowledgeBasesAsync()).IsFailed(out var error, out var knowledgeBases))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return false;
        }

        KnowledgeBases = knowledgeBases.ToList();
        NotifyStateChanged();
        return true;
    }

    private async Task<bool> LoadMarkdownGroupsAsync()
    {
        if ((await ragFacade.GetMarkdownGroupsAsync()).IsFailed(out var error, out var groups))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return false;
        }

        MarkdownGroups = groups.Select(group => group.Key).ToList();
        AreMarkdownGroupsLoaded = true;
        NotifyStateChanged();
        return true;
    }

    private async Task LoadDocumentQueueAsync(bool ensurePolling = true)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if ((await ragFacade.GetDocumentQueueAsync(SelectedKnowledgeBase.Id)).IsFailed(out var error, out var queue))
        {
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
            DocumentQueue = [];
        }
        else
        {
            DocumentQueue = queue.ToList();
        }

        if (ensurePolling)
        {
            EnsureQueuePolling();
        }

        NotifyStateChanged();
    }

    private bool HasIndexedContent(KnowledgeBase knowledgeBase)
    {
        if (knowledgeBase.DocumentCount > 0 || knowledgeBase.ChunkCount > 0)
        {
            return true;
        }

        return DocumentQueue.Any(item => item.Status == DocumentStatus.Done || item.ChunkCount > 0);
    }

    private async Task RefreshSelectedKnowledgeBaseAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            SelectedKnowledgeBaseVectorValidation = null;
            ClearBatchStartInFlight();
            ClearSingleIndexInFlight();
            NotifyStateChanged();
            return;
        }

        var selectedKnowledgeBaseId = SelectedKnowledgeBase.Id;
        await LoadKnowledgeBasesAsync();
        SelectedKnowledgeBase = KnowledgeBases.FirstOrDefault(kb =>
            string.Equals(kb.Id, selectedKnowledgeBaseId, StringComparison.OrdinalIgnoreCase));

        if (SelectedKnowledgeBase is null)
        {
            CurrentEmbeddingModelKey = string.Empty;
            DocumentQueue = [];
            SelectedKnowledgeBaseVectorValidation = null;
            ClearSingleIndexInFlight();
            NotifyStateChanged();
            return;
        }

        CurrentEmbeddingModelKey = HasEmbeddingBinding(SelectedKnowledgeBase)
            ? GetKnowledgeBaseModelKey(SelectedKnowledgeBase)
            : string.Empty;

        QueueSelectedKnowledgeBaseVectorValidation(SelectedKnowledgeBase.Id);
        NotifyStateChanged();
    }

    private void OnBatchIndexingProgress(IndexingProgress progress)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        _ = queuePollingState.RefreshFromProgressAsync(SelectedKnowledgeBase.Id);
    }

    private void EnsureQueuePolling()
    {
        if (SelectedKnowledgeBase is null)
        {
            queuePollingState.Stop();
            return;
        }

        queuePollingState.EnsurePolling(SelectedKnowledgeBase.Id, () => HasActiveQueueWork);
    }

    private bool EnsureEmbeddingModelConfiguredForIndexing()
    {
        if (SelectedKnowledgeBase is null)
        {
            return false;
        }

        if (HasEmbeddingBinding(SelectedKnowledgeBase))
        {
            return true;
        }

        snackbar.Add(localizer["RAG:EmbeddingModel:BindingRequired"], Severity.Warning);
        return false;
    }

    private async Task LoadSelectedKnowledgeBaseVectorValidationAsync(string knowledgeBaseId)
    {
        if (SelectedKnowledgeBase is null
            || !string.Equals(SelectedKnowledgeBase.Id, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
        {
            SelectedKnowledgeBaseVectorValidation = null;
            NotifyStateChanged();
            return;
        }

        var result = await ragFacade.GetKnowledgeBaseVectorValidationAsync(knowledgeBaseId);
        if (SelectedKnowledgeBase is null
            || !string.Equals(SelectedKnowledgeBase.Id, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (result.IsFailed(out var error, out var validation))
        {
            SelectedKnowledgeBaseVectorValidation = null;
            snackbar.Add($"{localizer["Common:Error"]}: {error.Message}", Severity.Error);
        }
        else
        {
            SelectedKnowledgeBaseVectorValidation = validation;
        }

        NotifyStateChanged();
    }

    private void UpdateLoadingState(string phaseText, double progressValue)
    {
        LoadingPhaseText = phaseText;
        LoadingProgressValue = progressValue;
        NotifyStateChanged();
    }

    private void QueueMarkdownGroupWarmup()
    {
        if (AreMarkdownGroupsLoaded || IsMarkdownGroupsLoading || SelectedKnowledgeBase is null)
        {
            return;
        }

        IsMarkdownGroupsLoading = true;
        NotifyStateChanged();
        _ = LoadMarkdownGroupsWarmupAsync();
    }

    private async Task LoadMarkdownGroupsWarmupAsync()
    {
        try
        {
            await LoadMarkdownGroupsAsync();
        }
        finally
        {
            IsMarkdownGroupsLoading = false;
            NotifyStateChanged();
        }
    }

    private void QueueSelectedKnowledgeBaseVectorValidation(string knowledgeBaseId)
    {
        _ = LoadSelectedKnowledgeBaseVectorValidationAsync(knowledgeBaseId);
    }

    private void OnQueueRefreshed(IReadOnlyList<DocumentQueueItem> queue)
    {
        DocumentQueue = queue.ToList();
        NotifyStateChanged();
    }

    private void OnQueueRefreshFailed(string message)
    {
        DocumentQueue = [];
        snackbar.Add($"{localizer["Common:Error"]}: {message}", Severity.Error);
        NotifyStateChanged();
    }

    private static bool IsCancellationMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && message.Contains("cancel", StringComparison.OrdinalIgnoreCase);

    private void ClearBatchStartInFlight()
    {
        _batchStartInFlightKnowledgeBaseId = null;
    }

    private void ClearSingleIndexInFlight()
    {
        _singleIndexInFlightKnowledgeBaseId = null;
        _singleIndexInFlightDocumentId = null;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ClearBatchStartInFlight();
        ClearSingleIndexInFlight();
        queuePollingState.Stop();

        if (_isAttached)
        {
            queuePollingState.QueueRefreshed -= OnQueueRefreshed;
            queuePollingState.QueueRefreshFailed -= OnQueueRefreshFailed;
            _isAttached = false;
        }
    }
}
