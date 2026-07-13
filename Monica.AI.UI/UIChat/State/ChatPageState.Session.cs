using Monica.AI.Models;
using Monica.AI.UI.UIChat.Support;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task EnsureSessionExistsAsync()
    {
        if (_workspace.Sessions.Count == 0)
        {
            _ = await TryCreateSessionAsync();
            return;
        }

        if (_workspace.CurrentSession is null)
        {
            _ = await _workspace.SelectSessionAsync(_workspace.Sessions.First().SessionId);
        }
    }

    /// <summary>
    /// Create a new chat session.
    /// </summary>
    public async Task CreateNewSessionAsync()
    {
        ClearError();
        _ = await TryCreateSessionAsync();
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    /// <summary>
    /// Select the current chat session.
    /// </summary>
    public async Task SelectSessionAsync(string sessionId)
    {
        ClearError();
        _ = await _workspace.SelectSessionAsync(sessionId);
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    /// <summary>
    /// Delete one chat session.
    /// </summary>
    public async Task DeleteSessionAsync(string sessionId)
    {
        var deletingCurrentSession = string.Equals(
            sessionId,
            _workspace.CurrentSessionId,
            StringComparison.Ordinal);
        if (!await _workspace.RemoveSessionAsync(sessionId))
        {
            return;
        }

        if (deletingCurrentSession)
        {
            if (_workspace.Sessions.Count == 0)
            {
                _ = await TryCreateSessionAsync();
            }
        }

        UpdateCurrentSession();
        NotifyStateChanged();
    }

    /// <summary>
    /// Confirms and clears every conversation in the current browser partition.
    /// </summary>
    public async Task ClearSessionsAsync()
    {
        if (_workspace.IsLoading || _workspace.Sessions.Count == 0)
        {
            return;
        }

        var confirmed = await _dialogService.ShowMessageBoxAsync(
            _localizer["Chat:History:Clear:Title"],
            _localizer["Chat:History:Clear:Message"],
            yesText: _localizer["Chat:History:Clear:Confirm"],
            noText: _localizer["Common:Actions:Cancel"],
            options: new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (confirmed != true || !await _workspace.ClearAsync())
        {
            return;
        }

        _ = await TryCreateSessionAsync();
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    private async Task<string?> EnsureCurrentSessionAsync()
    {
        var sessionId = _workspace.CurrentSessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            var session = await TryCreateSessionAsync();
            if (session == null)
            {
                return null;
            }

            sessionId = session.SessionId;
        }

        return sessionId;
    }

    private async Task<ChatSession?> TryCreateSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentProviderId))
        {
            SetPageError(_localizer["Provider:NoChatProvider"]);
            return null;
        }

        var createResult = await _chatFacade.CreateSessionAsync(
            CurrentProviderId,
            CurrentModelName,
            runtimeContext: BuildRuntimeContext(SelectedKnowledgeBaseIds));

        if (createResult.IsFailed(out var error, out var state))
        {
            SetPageError(error.Message ?? _localizer["Error:Generic"]);
            return null;
        }

        await _workspace.AddSessionAsync(state);
        ClearError();
        return state;
    }
}
