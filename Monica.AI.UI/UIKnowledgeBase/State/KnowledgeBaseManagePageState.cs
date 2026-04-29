using Microsoft.Extensions.Localization;
using Monica.AI.Abstractions;
using Monica.AI.KnowledgeBase.Facades;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Facades;
using Monica.AI.RAG.Models;
using Monica.AI.UI.Localization;
using Monica.Core.Results;
using MudBlazor;
using DocumentQueueItemModel = Monica.AI.KnowledgeBase.Models.DocumentQueueItem;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIKnowledgeBase.State;

/// <summary>
/// Owns mutable page state and UI orchestration for the knowledge-base management page.
/// </summary>
public sealed partial class KnowledgeBaseManagePageState
{
    private readonly KnowledgeBaseFacade _knowledgeBaseFacade;
    private readonly EmbeddingModelFacade _embeddingFacade;
    private readonly IAIProviderFactory _providerFactory;
    private readonly ISnackbar _snackbar;
    private readonly IDialogService _dialogService;
    private readonly IStringLocalizer<AIResource> _localizer;
    private bool _embeddingModelWarningShown;

    /// <summary>
    /// Initializes the page state and its collaborators.
    /// </summary>
    public KnowledgeBaseManagePageState(
        KnowledgeBaseFacade knowledgeBaseFacade,
        EmbeddingModelFacade embeddingFacade,
        IAIProviderFactory providerFactory,
        ISnackbar snackbar,
        IDialogService dialogService,
        IStringLocalizer<AIResource> localizer)
    {
        _knowledgeBaseFacade = knowledgeBaseFacade;
        _embeddingFacade = embeddingFacade;
        _providerFactory = providerFactory;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _localizer = localizer;
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
    /// Current knowledge-base selection.
    /// </summary>
    public KnowledgeBaseModel? SelectedKnowledgeBase { get; private set; }

    /// <summary>
    /// Available embedding model options.
    /// </summary>
    public IReadOnlyList<EmbeddingModelOption> AvailableModels { get; private set; } = [];

    /// <summary>
    /// Current embedding model key selected in the page.
    /// </summary>
    public string CurrentEmbeddingModelKey { get; private set; } = string.Empty;

    /// <summary>
    /// Current document inventory for the selected knowledge base.
    /// </summary>
    public IReadOnlyList<DocumentQueueItemModel> DocumentInventory { get; private set; } = [];

    /// <summary>
    /// Markdown groups available for import.
    /// </summary>
    public IReadOnlyList<string> MarkdownGroups { get; private set; } = [];

    /// <summary>
    /// Markdown group currently selected in the import card.
    /// </summary>
    public string? SelectedMarkdownGroup { get; set; }

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
    /// Whether all documents can be removed for the current selection.
    /// </summary>
    public bool CanClearKnowledgeBaseDocuments
        => SelectedKnowledgeBase is not null
           && DocumentInventory.Count > 0
           && DocumentInventory.All(document => document.Status != DocumentStatus.Indexing);

    /// <summary>
    /// Initializes the page for the current visit.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsInitialLoading = true;
        UpdateLoadingState(_localizer["RAG:KnowledgeBase:Title"].Value, 35);
        await LoadKnowledgeBasesAsync();
        await LoadEmbeddingModelsAsync();
        QueueMarkdownGroupWarmup();
        LoadingProgressValue = 100;
        IsInitialLoading = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Changes the current knowledge-base selection and loads dependent data.
    /// </summary>
    public async Task SelectKnowledgeBaseAsync(KnowledgeBaseModel? knowledgeBase)
    {
        SelectedKnowledgeBase = knowledgeBase;
        if (knowledgeBase is null)
        {
            CurrentEmbeddingModelKey = string.Empty;
            DocumentInventory = [];
            NotifyStateChanged();
            return;
        }

        IsSelectionLoading = true;
        UpdateLoadingState(_localizer["RAG:EmbeddingModel:Label"].Value, 28);
        NotifyStateChanged();

        if (!AreEmbeddingModelsLoaded)
        {
            await LoadEmbeddingModelsAsync();
        }

        CurrentEmbeddingModelKey = IsRagEnabled(knowledgeBase)
            ? GetKnowledgeBaseModelKey(knowledgeBase)
            : string.Empty;

        UpdateLoadingState(_localizer["KnowledgeBase:Documents:Title"].Value, 72);
        await LoadDocumentInventoryAsync();
        IsSelectionLoading = false;
        LoadingProgressValue = 100;
        QueueMarkdownGroupWarmup();
        NotifyStateChanged();
    }

    private async Task<bool> LoadKnowledgeBasesAsync()
    {
        if ((await _knowledgeBaseFacade.GetAllAsync()).IsFailed(out var error, out var knowledgeBases))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return false;
        }

        KnowledgeBases = knowledgeBases.ToList();
        NotifyStateChanged();
        return true;
    }

    private async Task<bool> LoadEmbeddingModelsAsync()
    {
        if ((await _embeddingFacade.GetEmbeddingModelsAsync()).IsFailed(out var error, out var models))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return false;
        }

        AvailableModels = models.ToList();
        AreEmbeddingModelsLoaded = true;

        if (AvailableModels.Count == 0 && !_embeddingModelWarningShown)
        {
            _embeddingModelWarningShown = true;
            _snackbar.Add(_localizer["RAG:EmbeddingModel:NotConfigured"], Severity.Warning);
        }

        NotifyStateChanged();
        return true;
    }

    private async Task<bool> LoadMarkdownGroupsAsync()
    {
        if ((await _knowledgeBaseFacade.GetMarkdownGroupsAsync()).IsFailed(out var error, out var groups))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return false;
        }

        MarkdownGroups = groups.Select(group => group.Key).ToList();
        AreMarkdownGroupsLoaded = true;
        NotifyStateChanged();
        return true;
    }

    private async Task LoadDocumentInventoryAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            DocumentInventory = [];
            NotifyStateChanged();
            return;
        }

        if ((await _knowledgeBaseFacade.GetDocumentInventoryAsync(SelectedKnowledgeBase.Id)).IsFailed(out var error, out var documents))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            DocumentInventory = [];
        }
        else
        {
            DocumentInventory = documents.ToList();
        }

        NotifyStateChanged();
    }

    private async Task RefreshSelectedKnowledgeBaseAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            NotifyStateChanged();
            return;
        }

        var selectedKnowledgeBaseId = SelectedKnowledgeBase.Id;
        await LoadKnowledgeBasesAsync();
        SelectedKnowledgeBase = KnowledgeBases.FirstOrDefault(kb =>
            string.Equals(kb.Id, selectedKnowledgeBaseId, StringComparison.OrdinalIgnoreCase));

        CurrentEmbeddingModelKey = SelectedKnowledgeBase is not null && IsRagEnabled(SelectedKnowledgeBase)
            ? GetKnowledgeBaseModelKey(SelectedKnowledgeBase)
            : string.Empty;

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
        if (AreMarkdownGroupsLoaded || IsMarkdownGroupsLoading)
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

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
