using Monica.AI.Models;
using Monica.AI.UI.UIChat.Support;
using Monica.Core.Results;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task EnsureSessionExistsAsync()
    {
        if (_sessionStore.Sessions.Count == 0)
        {
            _ = await TryCreateSessionAsync();
            return;
        }

        _sessionStore.CurrentSessionId = _sessionStore.Sessions.First().SessionId;
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
    public void SelectSession(string sessionId)
    {
        ClearError();
        _sessionStore.CurrentSessionId = sessionId;
    }

    /// <summary>
    /// Delete one chat session.
    /// </summary>
    public async Task DeleteSessionAsync(string sessionId)
    {
        var deletingCurrentSession = string.Equals(
            sessionId,
            _sessionStore.CurrentSessionId,
            StringComparison.Ordinal);
        _sessionStore.RemoveSession(sessionId);

        if (deletingCurrentSession)
        {
            if (_sessionStore.Sessions.Count == 0)
            {
                _ = await TryCreateSessionAsync();
            }
            else
            {
                _sessionStore.CurrentSessionId = _sessionStore.Sessions.First().SessionId;
            }
        }

        UpdateCurrentSession();
        NotifyStateChanged();
    }

    private async Task<string?> EnsureCurrentSessionAsync()
    {
        var sessionId = _sessionStore.CurrentSessionId;
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

        _sessionStore.AddSession(state);
        _sessionStore.CurrentSessionId = state.SessionId;
        ClearError();
        return state;
    }
}
