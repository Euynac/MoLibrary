using Monica.AI.KnowledgeBase.Models;
using Monica.Core.Results;
using MudBlazor;
using DocumentQueueItemModel = Monica.AI.KnowledgeBase.Models.DocumentQueueItem;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
    /// <summary>
    /// Initialize the page for the current visit.
    /// </summary>
    public async Task InitializeAsync()
    {
        Attach();

        IsInitialLoading = true;
        UpdateLoadingState(_localizer["RAG:KnowledgeBase:Title"].Value, 35);
        await LoadKnowledgeBasesAsync();
        LoadingProgressValue = 100;
        IsInitialLoading = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Change the current knowledge-base selection and load dependent data.
    /// </summary>
    public async Task SelectKnowledgeBaseAsync(KnowledgeBaseModel? knowledgeBase)
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
            DocumentQueue = [];
            SelectedKnowledgeBaseVectorValidation = null;
            ClearBatchStartInFlight();
            ClearSingleIndexInFlight();
            _queuePollingState.Stop();
            NotifyStateChanged();
            return;
        }

        IsSelectionLoading = true;
        UpdateLoadingState(_localizer["RAG:DocumentQueue:Title"].Value, 72);
        NotifyStateChanged();

        await LoadDocumentQueueAsync();
        EnsureQueuePolling();
        IsSelectionLoading = false;
        LoadingProgressValue = 100;
        NotifyStateChanged();
        QueueSelectedKnowledgeBaseVectorValidation(knowledgeBase.Id);
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

    private async Task LoadDocumentQueueAsync(bool ensurePolling = true)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if ((await _knowledgeBaseFacade.GetDocumentInventoryAsync(SelectedKnowledgeBase.Id)).IsFailed(out var error, out var queue))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            DocumentQueue = [];
        }
        else
        {
            DocumentQueue = queue.ToList();
        }

        RemoveUnavailableDocumentSelections();

        if (ensurePolling)
        {
            EnsureQueuePolling();
        }

        NotifyStateChanged();
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
            DocumentQueue = [];
            SelectedKnowledgeBaseVectorValidation = null;
            ClearSingleIndexInFlight();
            NotifyStateChanged();
            return;
        }

        QueueSelectedKnowledgeBaseVectorValidation(SelectedKnowledgeBase.Id);
        NotifyStateChanged();
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

        var result = await _ragFacade.GetKnowledgeBaseVectorValidationAsync(knowledgeBaseId);
        if (SelectedKnowledgeBase is null
            || !string.Equals(SelectedKnowledgeBase.Id, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (result.IsFailed(out var error, out var validation))
        {
            SelectedKnowledgeBaseVectorValidation = null;
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
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

    private void QueueSelectedKnowledgeBaseVectorValidation(string knowledgeBaseId)
    {
        _ = LoadSelectedKnowledgeBaseVectorValidationAsync(knowledgeBaseId);
    }

    private void OnQueueRefreshed(IReadOnlyList<DocumentQueueItemModel> queue)
    {
        DocumentQueue = queue.ToList();
        RemoveUnavailableDocumentSelections();
        NotifyStateChanged();
    }

    private void OnQueueRefreshFailed(string message)
    {
        DocumentQueue = [];
        _snackbar.Add($"{_localizer["Common:Error"]}: {message}", Severity.Error);
        NotifyStateChanged();
    }
}
