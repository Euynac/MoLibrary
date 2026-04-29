using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using MudBlazor;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
    /// <summary>
    /// Check whether one queue row can be indexed now.
    /// </summary>
    public bool CanStartDocumentIndexing(DocumentQueueItem document)
        => SelectedKnowledgeBase is not null
           && document.Status is DocumentStatus.Pending or DocumentStatus.Error
           && !HasActiveQueueWork;

    private void OnBatchIndexingProgress(IndexingProgress progress)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        _ = _queuePollingState.RefreshFromProgressAsync(SelectedKnowledgeBase.Id);
    }

    private void EnsureQueuePolling()
    {
        if (SelectedKnowledgeBase is null)
        {
            _queuePollingState.Stop();
            return;
        }

        _queuePollingState.EnsurePolling(SelectedKnowledgeBase.Id, () => HasActiveQueueWork);
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

        _snackbar.Add(_localizer["RAG:EmbeddingModel:BindingRequired"], Severity.Warning);
        return false;
    }

    private static bool IsCancellationMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && message.Contains("cancel", StringComparison.OrdinalIgnoreCase);
}
