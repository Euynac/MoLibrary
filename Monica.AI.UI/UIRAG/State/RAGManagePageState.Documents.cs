using Microsoft.AspNetCore.Components.Forms;
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
    /// Delete one queue document after confirmation.
    /// </summary>
    public async Task DeleteDocumentAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["Common:Confirm"],
            _localizer["RAG:DocumentQueue:DeleteConfirm", document.Name],
            yesText: _localizer["Common:Delete"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        if ((await _knowledgeBaseFacade.RemoveDocumentAsync(SelectedKnowledgeBase.Id, document.Id)).IsFailed(out var error))
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

    /// <summary>
    /// Open the markdown document-selection dialog for the current selection.
    /// </summary>
    public async Task ShowDocumentSelectionDialogAsync()
    {
        if (SelectedKnowledgeBase is null || string.IsNullOrEmpty(SelectedMarkdownGroup))
        {
            return;
        }

        if ((await _ragFacade.GetAvailableDocumentsAsync(SelectedMarkdownGroup)).IsFailed(out var error, out var documents))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
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

        var dialog = await _dialogService.ShowAsync<DocumentSelectionDialog>(
            _localizer["RAG:DocumentSelection:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: List<string> selectedDocumentIds })
        {
            return;
        }

        if ((await _ragFacade.AddDocumentsToQueueAsync(
                SelectedKnowledgeBase.Id,
                selectedDocumentIds,
                SelectedMarkdownGroup)).IsFailed(out var addError))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {addError.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
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
                _snackbar.Add(_localizer["RAG:Indexing:FileTooLarge"], Severity.Error);
                return;
            }

            using var stream = file.OpenReadStream(maxFileSize);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            if ((await _ragFacade.UploadAndIndexDocumentAsync(
                    SelectedKnowledgeBase.Id,
                    file.Name,
                    content)).IsFailed(out var error))
            {
                _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
                return;
            }

            _snackbar.Add(_localizer["Common:Success"], Severity.Success);
            await RefreshSelectedKnowledgeBaseAsync();
            await LoadDocumentQueueAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {ex.Message}", Severity.Error);
        }
    }
}
