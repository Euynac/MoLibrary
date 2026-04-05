using Monica.AI.RAG.Models;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
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
            _snackbar.Add(_localizer["RAG:ParallelIndexing:NoPendingDocuments"], Severity.Warning);
            return;
        }

        _batchStartInFlightKnowledgeBaseId = selectedKnowledgeBaseId;
        EnsureQueuePolling();
        NotifyStateChanged();

        var progress = new Progress<IndexingProgress>(OnBatchIndexingProgress);
        try
        {
            var result = await _ragFacade.StartBatchIndexingAsync(
                selectedKnowledgeBaseId,
                ParallelCount,
                progress);

            if (result.IsFailed(out var error))
            {
                if (IsCancellationMessage(error.Message))
                {
                    _snackbar.Add(_localizer["Error:RequestCancelled"], Severity.Warning);
                }
                else
                {
                    _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
                    await _dialogService.ShowMessageBoxAsync(
                        _localizer["Common:Error"],
                        error.Message ?? _localizer["Common:Error"].Value,
                        yesText: _localizer["Common:Close"]);
                }
            }
            else
            {
                _snackbar.Add(_localizer["Common:Success"], Severity.Success);
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
            var result = await _ragFacade.StartDocumentIndexingAsync(
                selectedKnowledgeBaseId,
                document.Id,
                progress);
            if (result.IsFailed(out var error))
            {
                _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
                return;
            }

            _snackbar.Add(_localizer["Common:Success"], Severity.Success);
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

        var cancelResult = await _ragFacade.CancelBatchIndexingAsync(SelectedKnowledgeBase.Id);
        if (cancelResult.IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        ClearBatchStartInFlight();
        EnsureQueuePolling();
        _snackbar.Add(cancelResult.Message ?? _localizer["Error:RequestCancelled"].Value, Severity.Warning);
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

        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["Common:Confirm"],
            _localizer["RAG:DocumentQueue:ClearDocumentsConfirm", SelectedKnowledgeBase.Name],
            yesText: _localizer["RAG:DocumentQueue:Actions:ClearDocuments"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        var result = await _ragFacade.ClearKnowledgeBaseDocumentsAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var removedCount))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["RAG:DocumentQueue:Messages:DocumentsCleared", removedCount], Severity.Success);
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

        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["Common:Confirm"],
            _localizer["RAG:DocumentQueue:ClearQueueConfirm", SelectedKnowledgeBase.Name],
            yesText: _localizer["RAG:DocumentQueue:Actions:ClearQueue"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        var result = await _ragFacade.ClearDocumentQueueAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var removedCount))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["RAG:DocumentQueue:Messages:QueueCleared", removedCount], Severity.Success);
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

        var result = await _ragFacade.ReindexKnowledgeBaseAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var queuedCount))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["RAG:VectorValidation:Messages:ReindexQueued", queuedCount], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }
}
