using Microsoft.Extensions.Localization;
using Monica.AI.Abstractions;
using Monica.AI.KnowledgeBase.Facades;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Facades;
using Monica.AI.RAG.Models;
using Monica.AI.UI.Localization;
using MudBlazor;
using DocumentQueueItemModel = Monica.AI.KnowledgeBase.Models.DocumentQueueItem;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIRAG.State;

/// <summary>
/// Owns mutable page state and UI orchestration for the RAG management page.
/// </summary>
public sealed partial class RAGManagePageState : IDisposable
{
    private readonly RAGFacade _ragFacade;
    private readonly EmbeddingModelFacade _embeddingFacade;
    private readonly KnowledgeBaseFacade _knowledgeBaseFacade;
    private readonly IAIProviderFactory _providerFactory;
    private readonly ISnackbar _snackbar;
    private readonly IDialogService _dialogService;
    private readonly IStringLocalizer<AIResource> _localizer;
    private readonly RAGQueuePollingState _queuePollingState;

    private bool _isAttached;
    private string? _batchStartInFlightKnowledgeBaseId;
    private string? _singleIndexInFlightKnowledgeBaseId;
    private string? _singleIndexInFlightDocumentId;

    /// <summary>
    /// Initializes the page state and its collaborators.
    /// </summary>
    public RAGManagePageState(
        RAGFacade ragFacade,
        EmbeddingModelFacade embeddingFacade,
        KnowledgeBaseFacade knowledgeBaseFacade,
        IAIProviderFactory providerFactory,
        ISnackbar snackbar,
        IDialogService dialogService,
        IStringLocalizer<AIResource> localizer,
        RAGQueuePollingState queuePollingState)
    {
        _ragFacade = ragFacade;
        _embeddingFacade = embeddingFacade;
        _knowledgeBaseFacade = knowledgeBaseFacade;
        _providerFactory = providerFactory;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _localizer = localizer;
        _queuePollingState = queuePollingState;
    }

    /// <summary>
    /// Raised when the page should re-render.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// All knowledge bases shown in the sidebar.
    /// </summary>
    public IReadOnlyList<KnowledgeBaseModel> KnowledgeBases { get; private set; } = [];

    /// <summary>
    /// Current knowledge base selection.
    /// </summary>
    public KnowledgeBaseModel? SelectedKnowledgeBase { get; private set; }

    /// <summary>
    /// Available embedding model options used to enable or reconfigure RAG indexing.
    /// </summary>
    public IReadOnlyList<EmbeddingModelOption> AvailableModels { get; private set; } = [];

    /// <summary>
    /// Current embedding model key selected in the RAG page.
    /// </summary>
    public string CurrentEmbeddingModelKey { get; private set; } = string.Empty;

    /// <summary>
    /// Current document queue for the selected knowledge base.
    /// </summary>
    public IReadOnlyList<DocumentQueueItemModel> DocumentQueue { get; private set; } = [];

    /// <summary>
    /// Maximum parallel indexing concurrency requested by the UI.
    /// </summary>
    public int ParallelCount { get; set; } = 5;

    /// <summary>
    /// Vector validation result for the current selection.
    /// </summary>
    public KnowledgeBaseVectorValidationResult? SelectedKnowledgeBaseVectorValidation { get; private set; }

    /// <summary>
    /// Whether embedding-model metadata was loaded once already.
    /// </summary>
    public bool AreEmbeddingModelsLoaded { get; private set; }

    /// <summary>
    /// Whether the first page load is still running.
    /// </summary>
    public bool IsInitialLoading { get; private set; } = true;

    /// <summary>
    /// Whether the current knowledge-base selection is still loading.
    /// </summary>
    public bool IsSelectionLoading { get; private set; }

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
           && HasEmbeddingBinding(SelectedKnowledgeBase)
           && SelectedDocumentIds.Count > 0
           && DocumentQueue.Any(document => SelectedDocumentIds.Contains(document.Id)
                                            && document.Status is DocumentStatus.Pending or DocumentStatus.Error)
           && !HasActiveQueueWork;

    /// <summary>
    /// Whether batch indexing can be cancelled for the current selection.
    /// </summary>
    public bool CanCancelBatchIndexing
        => SelectedKnowledgeBase is not null
           && IsBatchIndexingRunning;

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
    /// Whether RAG support can be enabled for the current knowledge base.
    /// </summary>
    public bool CanEnableRagSupport
        => SelectedKnowledgeBase is not null
           && !HasEmbeddingBinding(SelectedKnowledgeBase)
           && !string.IsNullOrWhiteSpace(CurrentEmbeddingModelKey)
           && !HasActiveQueueWork;

    /// <summary>
    /// Whether RAG support can be removed for the current knowledge base.
    /// </summary>
    public bool CanRemoveRagSupport
        => SelectedKnowledgeBase is not null
           && HasEmbeddingBinding(SelectedKnowledgeBase)
           && !HasActiveQueueWork;

    /// <summary>
    /// Selected document ids for batch indexing.
    /// </summary>
    public HashSet<string> SelectedDocumentIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether at least one indexable row exists for the current selection.
    /// </summary>
    public bool HasIndexableDocuments
        => DocumentQueue.Any(static document => document.Status is DocumentStatus.Pending or DocumentStatus.Error);

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

            return _ragFacade.IsBatchIndexingActive(SelectedKnowledgeBase.Id);
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

        _queuePollingState.QueueRefreshed += OnQueueRefreshed;
        _queuePollingState.QueueRefreshFailed += OnQueueRefreshFailed;
        _isAttached = true;
    }

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
        _queuePollingState.Stop();

        if (_isAttached)
        {
            _queuePollingState.QueueRefreshed -= OnQueueRefreshed;
            _queuePollingState.QueueRefreshFailed -= OnQueueRefreshFailed;
            _isAttached = false;
        }
    }
}
