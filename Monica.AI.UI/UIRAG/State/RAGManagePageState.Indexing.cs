using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Facades;
using Monica.AI.RAG.Models;
using Monica.AI.UI.UIRAG.Components;
using Monica.Core.Results;
using MudBlazor;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
    /// <summary>
    /// Changes the current embedding selection and immediately updates RAG support when already enabled.
    /// </summary>
    public async Task ChangeEmbeddingModelAsync(string modelKey)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        CurrentEmbeddingModelKey = modelKey;
        NotifyStateChanged();

        if (HasEmbeddingBinding(SelectedKnowledgeBase))
        {
            await EnableOrUpdateRagSupportAsync();
        }
    }

    /// <summary>
    /// Enables RAG support for the selected knowledge base or switches its embedding model.
    /// </summary>
    public async Task EnableOrUpdateRagSupportAsync()
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var selectedKnowledgeBaseId = SelectedKnowledgeBase.Id;
        var isNewRagBinding = !HasEmbeddingBinding(SelectedKnowledgeBase);

        if (!EmbeddingModelOption.TryParseModelKey(CurrentEmbeddingModelKey, out var providerId, out var modelName))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: invalid embedding model key.", Severity.Error);
            return;
        }

        var previousModelKey = GetKnowledgeBaseModelKey(SelectedKnowledgeBase);
        if (HasEmbeddingBinding(SelectedKnowledgeBase)
            && string.Equals(previousModelKey, CurrentEmbeddingModelKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var requiresReindexWarning = HasIndexedContent(SelectedKnowledgeBase)
            && !string.IsNullOrWhiteSpace(previousModelKey);
        if (requiresReindexWarning)
        {
            var confirmed = await _dialogService.ShowMessageBoxAsync(
                _localizer["RAG:EmbeddingModel:ReindexConfirm:Title"],
                _localizer["RAG:EmbeddingModel:ReindexConfirm:Message", SelectedKnowledgeBase.Name],
                yesText: _localizer["RAG:EmbeddingModel:ReindexConfirm:Continue"],
                cancelText: _localizer["Common:Cancel"]);

            if (confirmed != true)
            {
                CurrentEmbeddingModelKey = previousModelKey;
                NotifyStateChanged();
                return;
            }
        }

        if (!TryBeginRagSupportAction(selectedKnowledgeBaseId, RagSupportActionKind.EnableOrUpdate))
        {
            return;
        }

        try
        {
            if (isNewRagBinding && !await EnsureVectorCollectionReusableAsync(selectedKnowledgeBaseId))
            {
                return;
            }

            if ((await _embeddingFacade.SetKnowledgeBaseEmbeddingModelAsync(
                    selectedKnowledgeBaseId,
                    providerId,
                    modelName,
                    clearIndex: true)).IsFailed(out var error))
            {
                CurrentEmbeddingModelKey = previousModelKey;
                _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
                NotifyStateChanged();
                return;
            }

            await RefreshSelectedKnowledgeBaseAsync();
            await LoadDocumentQueueAsync();

            _snackbar.Add(
                requiresReindexWarning
                    ? _localizer["RAG:EmbeddingModel:ReindexRequired"]
                    : _localizer["RAG:RagSupport:Enabled"],
                requiresReindexWarning ? Severity.Warning : Severity.Success);
        }
        finally
        {
            EndRagSupportAction(selectedKnowledgeBaseId);
        }
    }

    /// <summary>
    /// Removes RAG support and clears existing RAG vector/index data for the selected knowledge base.
    /// </summary>
    public async Task RemoveRagSupportAsync()
    {
        if (SelectedKnowledgeBase is null || !HasEmbeddingBinding(SelectedKnowledgeBase))
        {
            return;
        }

        var selectedKnowledgeBaseId = SelectedKnowledgeBase.Id;
        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["RAG:RagSupport:RemoveConfirm:Title"],
            _localizer["RAG:RagSupport:RemoveConfirm:Message", SelectedKnowledgeBase.Name],
            yesText: _localizer["RAG:RagSupport:RemoveConfirm:Confirm"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        if (!TryBeginRagSupportAction(selectedKnowledgeBaseId, RagSupportActionKind.Remove))
        {
            return;
        }

        try
        {
            var result = await _ragFacade.RemoveKnowledgeBaseRagSupportAsync(selectedKnowledgeBaseId);
            if (result.IsFailed(out var error, out var removal))
            {
                if (CanForceRemoveRagSupport(error)
                    && await ConfirmForceRemoveRagSupportAsync())
                {
                    await ForceRemoveRagSupportAsync(selectedKnowledgeBaseId);
                    return;
                }

                _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
                return;
            }

            await CompleteRagSupportRemovalAsync(removal);
        }
        finally
        {
            EndRagSupportAction(selectedKnowledgeBaseId);
        }
    }

    /// <summary>
    /// Opens the embedding model diagnostics dialog.
    /// </summary>
    public async Task ShowEmbeddingDiagnosticsDialogAsync()
    {
        await _dialogService.ShowAsync<EmbeddingDiagnosticsDialog>(
            _localizer["RAG:EmbeddingDiagnostics:Title"],
            new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true });
    }

    /// <summary>
    /// Opens the vector-store diagnostics dialog.
    /// </summary>
    public async Task ShowVectorStoreDiagnosticsDialogAsync()
    {
        await _dialogService.ShowAsync<VectorStoreDiagnosticsDialog>(
            _localizer["RAG:VectorDiagnostics:Title"],
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
    }

    /// <summary>
    /// Start batch indexing for selected pending or failed queue rows.
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
        if (!await EnsureVectorCollectionReusableAsync(selectedKnowledgeBaseId))
        {
            return;
        }

        var selectedDocuments = DocumentQueue
            .Where(document => SelectedDocumentIds.Contains(document.Id)
                               && document.Status is DocumentStatus.Pending or DocumentStatus.Error)
            .ToList();
        if (selectedDocuments.Count == 0)
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
                selectedDocuments.Select(static document => document.Id),
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
        if (!await EnsureVectorCollectionReusableAsync(selectedKnowledgeBaseId))
        {
            return;
        }

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

    private bool HasIndexedContent(KnowledgeBaseModel knowledgeBase)
    {
        if (knowledgeBase.DocumentCount > 0 || knowledgeBase.ChunkCount > 0)
        {
            return true;
        }

        return DocumentQueue.Any(static item => item.Status == DocumentStatus.Done || item.ChunkCount > 0);
    }

    private async Task<bool> EnsureVectorCollectionReusableAsync(string knowledgeBaseId)
    {
        var knowledgeBase = SelectedKnowledgeBase;
        if (knowledgeBase is null)
        {
            return false;
        }

        if (HasIndexedContent(knowledgeBase))
        {
            return true;
        }

        var statusResult = await _ragFacade.GetKnowledgeBaseVectorCollectionStatusAsync(knowledgeBaseId);
        if (statusResult.IsFailed(out var statusError, out var status))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {statusError.Message}", Severity.Error);
            return false;
        }

        if (!status.WasChecked)
        {
            _snackbar.Add(_localizer["RAG:VectorCollection:CheckUnavailable"], Severity.Warning);
            return true;
        }

        if (!status.Exists)
        {
            return true;
        }

        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["RAG:VectorCollection:OverwriteConfirm:Title"],
            _localizer["RAG:VectorCollection:OverwriteConfirm:Message", status.CollectionName],
            yesText: _localizer["RAG:VectorCollection:OverwriteConfirm:Confirm"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return false;
        }

        var overwriteResult = await _ragFacade.OverwriteKnowledgeBaseVectorCollectionAsync(knowledgeBaseId);
        if (overwriteResult.IsFailed(out var overwriteError, out var overwrite))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {overwriteError.Message}", Severity.Error);
            return false;
        }

        SelectedKnowledgeBaseVectorValidation = null;
        _snackbar.Add(
            _localizer[
                "RAG:VectorCollection:Overwritten",
                overwrite.CollectionName,
                overwrite.ResetDocumentCount,
                overwrite.ClearedChunkCount],
            Severity.Warning);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
        return true;
    }

    private async Task<bool> ConfirmForceRemoveRagSupportAsync()
    {
        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["RAG:RagSupport:ForceRemoveConfirm:Title"],
            _localizer["RAG:RagSupport:ForceRemoveConfirm:Message"],
            yesText: _localizer["RAG:RagSupport:ForceRemoveConfirm:Confirm"],
            cancelText: _localizer["Common:Cancel"]);

        return confirmed == true;
    }

    private async Task ForceRemoveRagSupportAsync(string knowledgeBaseId)
    {
        var forcedResult = await _ragFacade.RemoveKnowledgeBaseRagSupportAsync(
            knowledgeBaseId,
            forceLocalMetadataRemoval: true);
        if (forcedResult.IsFailed(out var forceError, out var forcedRemoval))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {forceError.Message}", Severity.Error);
            return;
        }

        await CompleteRagSupportRemovalAsync(forcedRemoval);
    }

    private async Task CompleteRagSupportRemovalAsync(KnowledgeBaseRagSupportRemovalResult removal)
    {
        CurrentEmbeddingModelKey = string.Empty;
        SelectedKnowledgeBaseVectorValidation = null;
        _snackbar.Add(
            _localizer[
                removal.WasForced
                    ? "RAG:RagSupport:ForceRemoved"
                    : "RAG:RagSupport:Removed",
                removal.ResetDocumentCount,
                removal.ClearedChunkCount],
            removal.WasForced ? Severity.Warning : Severity.Success);

        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    private static bool CanForceRemoveRagSupport(Res error)
    {
        if (error.Metadata is null)
        {
            return false;
        }

        var metadata = (IDictionary<string, object?>)error.Metadata;
        return metadata.TryGetValue(RAGFacade.CAN_FORCE_REMOVE_RAG_SUPPORT_METADATA_KEY, out var canForce)
               && canForce is true;
    }
}
