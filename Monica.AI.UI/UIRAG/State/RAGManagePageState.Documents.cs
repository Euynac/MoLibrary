using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.AI.UI.UIRAG.Components;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
    /// <summary>
    /// Open the chunk viewer dialog for one queue row.
    /// </summary>
    public async Task ShowChunkViewerAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var result = await _ragFacade.GetDocumentChunksAsync(SelectedKnowledgeBase.Id, document.Id);
        if (result.IsFailed(out var error, out var view))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        var statusText = document.Status switch
        {
            DocumentStatus.Done => _localizer["RAG:ChunkViewer:Indexed", document.IndexedAt?.ToString("yyyy-MM-dd") ?? ""].Value,
            DocumentStatus.Pending => _localizer["RAG:ChunkViewer:PendingIndexing"].Value,
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

        await _dialogService.ShowAsync<ChunkViewerDialog>(
            _localizer["RAG:ChunkViewer:Title"],
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

        if ((await _ragFacade.ReindexDocumentAsync(SelectedKnowledgeBase.Id, document.Id)).IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentQueueAsync();
    }

    /// <summary>
    /// Show the full indexing error for one queue row.
    /// </summary>
    public async Task ShowIndexingErrorAsync(DocumentQueueItem document)
    {
        var message = string.IsNullOrWhiteSpace(document.ErrorMessage)
            ? _localizer["Common:Error"].Value
            : document.ErrorMessage;

        await _dialogService.ShowMessageBoxAsync(
            _localizer["Common:Error"],
            $"{document.Name}\n\n{message}",
            yesText: _localizer["Common:Close"]);
    }
}
