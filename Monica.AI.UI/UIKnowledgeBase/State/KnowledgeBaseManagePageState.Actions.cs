using Microsoft.AspNetCore.Components.Forms;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.UI.UIKnowledgeBase.Components;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIKnowledgeBase.State;

public sealed partial class KnowledgeBaseManagePageState
{
    /// <summary>
    /// Deletes the current knowledge-base selection if present.
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
    /// Deletes one knowledge base after confirmation.
    /// </summary>
    public async Task DeleteKnowledgeBaseAsync(Monica.AI.KnowledgeBase.Models.KnowledgeBase knowledgeBase)
    {
        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["Common:Confirm"],
            _localizer["KnowledgeBase:Manage:DeleteConfirm", knowledgeBase.Name],
            yesText: _localizer["Common:Delete"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        if ((await _knowledgeBaseFacade.DeleteAsync(knowledgeBase.Id)).IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
        if (string.Equals(SelectedKnowledgeBase?.Id, knowledgeBase.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedKnowledgeBase = null;
            DocumentInventory = [];
        }

        await LoadKnowledgeBasesAsync();
        NotifyStateChanged();
    }

    /// <summary>
    /// Opens the create knowledge-base dialog.
    /// </summary>
    public async Task ShowCreateKnowledgeBaseDialogAsync()
    {
        var dialog = await _dialogService.ShowAsync<CreateKnowledgeBaseDialog>(
            _localizer["RAG:Manage:CreateDialog:Title"],
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: KnowledgeBaseDialogResult data })
        {
            return;
        }

        if ((await _knowledgeBaseFacade.CreateAsync(data.Id, data.Name, data.Description)).IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
        await LoadKnowledgeBasesAsync();
    }

    /// <summary>
    /// Opens the edit knowledge-base dialog for the current selection.
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
            { nameof(CreateKnowledgeBaseDialog.SubmitText), _localizer["Common:Actions:Save"].Value },
            { nameof(CreateKnowledgeBaseDialog.IsIdReadOnly), true }
        };

        var dialog = await _dialogService.ShowAsync<CreateKnowledgeBaseDialog>(
            _localizer["RAG:Manage:EditDialog:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: KnowledgeBaseDialogResult data })
        {
            return;
        }

        if ((await _knowledgeBaseFacade.UpdateAsync(
                SelectedKnowledgeBase.Id,
                data.Name,
                data.Description)).IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
    }

    /// <summary>
    /// Opens the markdown document import dialog for the current selection.
    /// </summary>
    public async Task ShowDocumentImportDialogAsync()
    {
        if (SelectedKnowledgeBase is null || string.IsNullOrEmpty(SelectedMarkdownGroup))
        {
            return;
        }

        if ((await _knowledgeBaseFacade.GetMarkdownDocumentsAsync(SelectedMarkdownGroup)).IsFailed(out var error, out var documents))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        var existingDocumentIds = DocumentInventory
            .Select(document => document.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parameters = new DialogParameters
        {
            { nameof(DocumentSelectionDialog.AvailableDocuments), documents.ToList() },
            { nameof(DocumentSelectionDialog.ExistingDocumentIds), existingDocumentIds }
        };

        var dialog = await _dialogService.ShowAsync<DocumentSelectionDialog>(
            _localizer["KnowledgeBase:DocumentImport:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: List<string> selectedDocumentIds })
        {
            return;
        }

        if ((await _knowledgeBaseFacade.ImportMarkdownDocumentsAsync(
                SelectedKnowledgeBase.Id,
                SelectedMarkdownGroup,
                selectedDocumentIds)).IsFailed(out var addError, out var importResult))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {addError.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(
            _localizer["KnowledgeBase:DocumentImport:Imported", importResult.AddedCount, importResult.SkippedCount],
            Severity.Success);
        await LoadDocumentInventoryAsync();
    }

    /// <summary>
    /// Uploads one file as a pending knowledge-base document.
    /// </summary>
    public async Task UploadFileAsync(IBrowserFile file)
    {
        if (SelectedKnowledgeBase is null)
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

            if ((await _knowledgeBaseFacade.UploadDocumentAsync(
                    SelectedKnowledgeBase.Id,
                    file.Name,
                    content)).IsFailed(out var error))
            {
                _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
                return;
            }

            _snackbar.Add(_localizer["Common:Success"], Severity.Success);
            await LoadDocumentInventoryAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// Deletes one document after confirmation.
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
        await LoadDocumentInventoryAsync();
    }

    /// <summary>
    /// Opens the raw source preview for one knowledge-base document.
    /// </summary>
    public async Task ShowDocumentPreviewAsync(DocumentQueueItem document)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        var result = await _knowledgeBaseFacade.GetDocumentPreviewAsync(SelectedKnowledgeBase.Id, document.Id);
        if (result.IsFailed(out var error, out var preview))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        var parameters = new DialogParameters
        {
            { nameof(KnowledgeBaseDocumentPreviewDialog.DocumentName), preview.DocumentName },
            { nameof(KnowledgeBaseDocumentPreviewDialog.DocumentPath), preview.DocumentId },
            { nameof(KnowledgeBaseDocumentPreviewDialog.Content), preview.Content },
            { nameof(KnowledgeBaseDocumentPreviewDialog.SourceKind), preview.SourceKind },
            { nameof(KnowledgeBaseDocumentPreviewDialog.SourceGroupKey), preview.SourceGroupKey }
        };

        await _dialogService.ShowAsync<KnowledgeBaseDocumentPreviewDialog>(
            _localizer["KnowledgeBase:Documents:Preview:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true });
    }

    /// <summary>
    /// Clears all documents after confirmation.
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

        var result = await _knowledgeBaseFacade.ClearDocumentsAsync(SelectedKnowledgeBase.Id);
        if (result.IsFailed(out var error, out var removedCount))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["RAG:DocumentQueue:Messages:DocumentsCleared", removedCount], Severity.Success);
        await RefreshSelectedKnowledgeBaseAsync();
        await LoadDocumentInventoryAsync();
    }

    /// <summary>
    /// Shows the full indexing error for one document.
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
