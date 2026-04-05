using Monica.AI.RAG.Models;
using Monica.AI.UI.UIRAG.Components;
using Monica.AI.UI.UIRAG.Models;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIRAG.State;

public sealed partial class RAGManagePageState
{
    /// <summary>
    /// Delete the current knowledge-base selection if present.
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
    /// Delete one knowledge base after confirmation.
    /// </summary>
    public async Task DeleteKnowledgeBaseAsync(KnowledgeBase knowledgeBase)
    {
        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["Common:Confirm"],
            $"Are you sure you want to delete knowledge base '{knowledgeBase.Name}'? This will remove all indexed documents.",
            yesText: _localizer["Common:Delete"],
            cancelText: _localizer["Common:Cancel"]);

        if (confirmed != true)
        {
            return;
        }

        if ((await _ragFacade.DeleteKnowledgeBaseAsync(knowledgeBase.Id)).IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
        if (string.Equals(SelectedKnowledgeBase?.Id, knowledgeBase.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedKnowledgeBase = null;
            SelectedKnowledgeBaseVectorValidation = null;
            CurrentEmbeddingModelKey = string.Empty;
            DocumentQueue = [];
            ClearBatchStartInFlight();
            ClearSingleIndexInFlight();
            _queuePollingState.Stop();
        }

        await LoadKnowledgeBasesAsync();
        NotifyStateChanged();
    }

    /// <summary>
    /// Change the embedding binding for the current knowledge base.
    /// </summary>
    public async Task ChangeEmbeddingModelAsync(string modelKey)
    {
        if (SelectedKnowledgeBase is null)
        {
            return;
        }

        if (!EmbeddingModelOption.TryParseModelKey(modelKey, out var providerId, out var modelName))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: invalid embedding model key.", Severity.Error);
            return;
        }

        var previousModelKey = GetKnowledgeBaseModelKey(SelectedKnowledgeBase);
        if (string.Equals(previousModelKey, modelKey, StringComparison.OrdinalIgnoreCase))
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

        CurrentEmbeddingModelKey = modelKey;
        NotifyStateChanged();

        if ((await _embeddingFacade.SetKnowledgeBaseEmbeddingModelAsync(
                SelectedKnowledgeBase.Id,
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

        if (SelectedKnowledgeBase is not null)
        {
            await LoadDocumentQueueAsync();
        }

        _snackbar.Add(
            requiresReindexWarning
                ? _localizer["RAG:EmbeddingModel:ReindexRequired"]
                : _localizer["Common:Success"],
            requiresReindexWarning ? Severity.Warning : Severity.Success);
    }

    /// <summary>
    /// Open the create knowledge-base dialog.
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

        if ((await _ragFacade.CreateKnowledgeBaseAsync(data.Id, data.Name, data.Description)).IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Common:Error"]}: {error.Message}", Severity.Error);
            return;
        }

        _snackbar.Add(_localizer["Common:Success"], Severity.Success);
        await LoadKnowledgeBasesAsync();
    }

    /// <summary>
    /// Open the edit knowledge-base dialog for the current selection.
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

        if ((await _ragFacade.UpdateKnowledgeBaseAsync(
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
}
