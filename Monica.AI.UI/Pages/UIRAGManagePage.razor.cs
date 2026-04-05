using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Monica.AI.RAG.Models;
using Monica.AI.UI.UIRAG.State;

namespace Monica.AI.UI.Pages;

public partial class UIRAGManagePage : IDisposable
{
    public const string PAGE_URL = "/ai/rag/manage";

    [Inject]
    public required RAGManagePageState PageState { get; set; }

    private IReadOnlyList<KnowledgeBase> _knowledgeBases => PageState.KnowledgeBases;
    private KnowledgeBase? _selectedKB => PageState.SelectedKnowledgeBase;
    private string _currentEmbeddingModelKey => PageState.CurrentEmbeddingModelKey;
    private IReadOnlyList<EmbeddingModelOption> _availableModels => PageState.AvailableModels;
    private IReadOnlyList<DocumentQueueItem> _documentQueue => PageState.DocumentQueue;
    private int _parallelCount
    {
        get => PageState.ParallelCount;
        set => PageState.ParallelCount = value;
    }
    private IReadOnlyList<string> _markdownGroups => PageState.MarkdownGroups;
    private string? _selectedMarkdownGroup
    {
        get => PageState.SelectedMarkdownGroup;
        set => PageState.SelectedMarkdownGroup = value;
    }
    private KnowledgeBaseVectorValidationResult? _selectedKbVectorValidation
        => PageState.SelectedKnowledgeBaseVectorValidation;
    private bool _isInitialLoading => PageState.IsInitialLoading;
    private bool _isSelectionLoading => PageState.IsSelectionLoading;
    private bool _isMarkdownGroupsLoading => PageState.IsMarkdownGroupsLoading;
    private string _loadingPhaseText => PageState.LoadingPhaseText;
    private double _loadingProgressValue => PageState.LoadingProgressValue;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        PageState.StateChanged += OnStateChanged;
        await PageState.InitializeAsync();
    }

    private void OnStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private Task OnKBSelected(KnowledgeBase? kb) => PageState.SelectKnowledgeBaseAsync(kb);
    private Task DeleteSelectedKnowledgeBase() => PageState.DeleteSelectedKnowledgeBaseAsync();
    private Task OnEmbeddingModelChanged(string modelKey) => PageState.ChangeEmbeddingModelAsync(modelKey);
    private static bool HasEmbeddingBinding(KnowledgeBase kb) => RAGManagePageState.HasEmbeddingBinding(kb);
    private string GetKnowledgeBaseModelDisplay(KnowledgeBase kb) => PageState.GetKnowledgeBaseModelDisplay(kb);
    private string GetKnowledgeBaseModelKey(KnowledgeBase kb) => PageState.GetKnowledgeBaseModelKey(kb);
    private string GetProviderDisplayLabel(string? providerId, string? modelName = null) => PageState.GetProviderDisplayLabel(providerId, modelName);
    private string GetModelDisplayByKey(string modelKey) => PageState.GetModelDisplayByKey(modelKey);
    private bool ShouldShowMissingVectorWarning() => PageState.ShouldShowMissingVectorWarning;
    private string GetMissingVectorWarningMessage() => PageState.GetMissingVectorWarningMessage();
    private bool CanReindexSelectedKnowledgeBase() => PageState.CanReindexSelectedKnowledgeBase;
    private Task ReindexSelectedKnowledgeBase() => PageState.ReindexSelectedKnowledgeBaseAsync();
    private Task ShowCreateKBDialog() => PageState.ShowCreateKnowledgeBaseDialogAsync();
    private Task ShowEditKBDialog() => PageState.ShowEditKnowledgeBaseDialogAsync();
    private bool CanStartBatchIndexing() => PageState.CanStartBatchIndexing;
    private bool CanCancelBatchIndexing() => PageState.CanCancelBatchIndexing;
    private Task StartBatchIndexing() => PageState.StartBatchIndexingAsync();
    private Task CancelBatchIndexing() => PageState.CancelBatchIndexingAsync();
    private bool CanStartDocumentIndexing(DocumentQueueItem document) => PageState.CanStartDocumentIndexing(document);
    private Task StartDocumentIndexing(DocumentQueueItem document) => PageState.StartDocumentIndexingAsync(document);
    private bool CanClearKnowledgeBaseDocuments() => PageState.CanClearKnowledgeBaseDocuments;
    private Task ClearKnowledgeBaseDocuments() => PageState.ClearKnowledgeBaseDocumentsAsync();
    private bool CanClearDocumentQueue() => PageState.CanClearDocumentQueue;
    private Task ClearDocumentQueue() => PageState.ClearDocumentQueueAsync();
    private Task ShowChunkViewer(DocumentQueueItem document) => PageState.ShowChunkViewerAsync(document);
    private Task ReindexDocument(DocumentQueueItem document) => PageState.ReindexDocumentAsync(document);
    private Task DeleteDocument(DocumentQueueItem document) => PageState.DeleteDocumentAsync(document);
    private Task ShowIndexingError(DocumentQueueItem document) => PageState.ShowIndexingErrorAsync(document);
    private Task ShowDocumentSelectionDialog() => PageState.ShowDocumentSelectionDialogAsync();
    private Task OnFileSelected(IBrowserFile file) => PageState.UploadFileAsync(file);

    public void Dispose()
    {
        PageState.StateChanged -= OnStateChanged;
        PageState.Dispose();
    }
}
