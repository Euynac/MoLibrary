using Monica.AI.Models;
using Monica.AI.UI.UIChat.Support;
using Monica.AI.UI.UIProvider.Components;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task LoadPersistedPreferencesAsync()
    {
        var savedProviderId = await _browserStorage.GetDefaultProviderAsync();
        var savedModelName = await _browserStorage.GetDefaultModelAsync();
        ToolDebugEnabled = await _browserStorage.GetToolDebugEnabledAsync();

        if (string.IsNullOrEmpty(savedProviderId))
        {
            return;
        }

        var provider = ChatProviderResolver.FindProvider(Providers, savedProviderId);
        if (provider != null)
        {
            ApplyProviderSelection(provider, savedModelName);
            return;
        }

        await ClearInvalidPreferencesAsync();
    }

    private void ApplyProviderSelection(AIProviderInfo provider, string? savedModelName)
    {
        DefaultProviderId = provider.ProviderId;
        CurrentProviderName = provider.DisplayName;
        CurrentProviderModels = ChatProviderResolver.GetChatModels(provider);

        if (ChatProviderResolver.IsChatModelValid(provider, savedModelName))
        {
            DefaultModelName = savedModelName;
        }
        else
        {
            DefaultModelName = ChatProviderResolver.GetPreferredChatModel(provider, provider.DefaultModel);
            if (!string.IsNullOrEmpty(savedModelName))
            {
                _ = _browserStorage.RemoveAsync("ai-chat:default-model");
            }
        }

        SupportsReasoning = ChatProviderResolver.GetReasoningSupport(
            Providers,
            DefaultProviderId,
            DefaultModelName);
    }

    private async Task ClearInvalidPreferencesAsync()
    {
        await _browserStorage.RemoveAsync("ai-chat:default-provider");
        await _browserStorage.RemoveAsync("ai-chat:default-model");
    }

    /// <summary>
    /// Change the current provider selection.
    /// </summary>
    public async Task ChangeProviderAsync(string providerId)
    {
        var provider = ChatProviderResolver.FindProvider(Providers, providerId);
        if (provider == null)
        {
            return;
        }

        var resolvedModelName = ChatProviderResolver.GetPreferredChatModel(provider, provider.DefaultModel);

        DefaultProviderId = providerId;
        DefaultModelName = resolvedModelName;
        CurrentProviderName = provider.DisplayName;
        CurrentProviderModels = ChatProviderResolver.GetChatModels(provider);
        SupportsReasoning = ChatProviderResolver.GetReasoningSupport(
            Providers,
            providerId,
            resolvedModelName);

        await _browserStorage.SaveDefaultProviderAsync(providerId);
        if (!string.IsNullOrEmpty(resolvedModelName))
        {
            await _browserStorage.SaveDefaultModelAsync(resolvedModelName);
        }
        else
        {
            await _browserStorage.RemoveAsync("ai-chat:default-model");
        }

        var currentSession = CurrentSession;
        if (currentSession != null)
        {
            _ = _chatFacade.UpdateSettings(
                currentSession,
                currentSession.Settings with
                {
                    ProviderId = providerId,
                    ModelName = resolvedModelName
                });
        }

        NotifyStateChanged();
    }

    private async Task ChangeModelAsync(string modelName)
    {
        DefaultModelName = modelName;
        await _browserStorage.SaveDefaultModelAsync(modelName);

        var currentSession = CurrentSession;
        if (currentSession != null)
        {
            _ = _chatFacade.UpdateSettings(
                currentSession,
                currentSession.Settings with { ModelName = modelName });
        }

        SupportsReasoning = ChatProviderResolver.GetReasoningSupport(
            Providers,
            CurrentProviderId,
            modelName);

        NotifyStateChanged();
    }

    /// <summary>
    /// Open the system-prompt settings dialog for the current provider.
    /// </summary>
    public async Task OpenPromptSettingsAsync()
    {
        var provider = ChatProviderResolver.FindProvider(Providers, CurrentProviderId)
                       ?? Providers.FirstOrDefault();
        if (provider == null)
        {
            _snackbar.Add(_localizer["Provider:NotFound"], Severity.Warning);
            return;
        }

        var session = CurrentSession;
        var hasStarted = session?.Messages.Count > 0;
        var currentPrompt = hasStarted ? session?.SystemPrompt : provider.SystemPrompt;

        var parameters = new DialogParameters
        {
            [nameof(ProviderSystemPromptDialog.ProviderName)] = provider.DisplayName,
            [nameof(ProviderSystemPromptDialog.SystemPrompt)] = currentPrompt,
            [nameof(ProviderSystemPromptDialog.IsReadOnly)] = hasStarted
        };

        var dialog = await _dialogService.ShowAsync<ProviderSystemPromptDialog>(
            _localizer["Provider:Settings:SystemPrompt"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is not { Canceled: false } || hasStarted)
        {
            return;
        }

        var prompt = result.Data as string;
        var updateResult = _providerFacade.UpdateProviderSystemPrompt(provider.ProviderId, prompt);
        if (updateResult.IsFailed(out var error))
        {
            _snackbar.Add($"{_localizer["Provider:Settings:UpdateFailed"]}: {error.Message}", Severity.Error);
            return;
        }

        if (session != null && session.Messages.Count == 0)
        {
            _ = _chatFacade.UpdateSettings(
                session,
                session.Settings with { SystemPrompt = prompt });
        }

        Providers = ChatProviderResolver.GetChatProviders(_chatFacade.GetProviders());
        _snackbar.Add(_localizer["Provider:Settings:UpdateSuccess"], Severity.Success);
        NotifyStateChanged();
    }

    private void SetReasoningEnabled(bool enabled)
    {
        ReasoningEnabled = enabled;
        NotifyStateChanged();
    }

    private void SetSelectedKnowledgeBases(List<string> selectedIds)
    {
        SelectedKnowledgeBaseIds = selectedIds;

        var currentSession = CurrentSession;
        if (currentSession == null)
        {
            return;
        }

        _ = _chatFacade.UpdateRuntimeContext(currentSession, BuildRuntimeContext(selectedIds));
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    /// <summary>
    /// Toggle tool-call debug display.
    /// </summary>
    public async Task ToggleToolDebugAsync()
    {
        ToolDebugEnabled = !ToolDebugEnabled;
        await _browserStorage.SaveToolDebugEnabledAsync(ToolDebugEnabled);
        NotifyStateChanged();
    }

    private async Task LoadKnowledgeBasesAsync()
    {
        if (!_options.ShowKnowledgeBaseSelector)
        {
            return;
        }

        var knowledgeBaseResult = await _knowledgeBaseFacade.GetAllAsync();
        if (knowledgeBaseResult.IsFailed(out var error, out var knowledgeBases))
        {
            SetPageError(error.Message ?? _localizer["Error:Generic"], showSnackbar: false);
            KnowledgeBases = [];
            return;
        }

        KnowledgeBases = knowledgeBases;
    }
}
