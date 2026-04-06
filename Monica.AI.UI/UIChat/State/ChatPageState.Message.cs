using System.Net.Http;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Support;
using MudBlazor;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsSending)
        {
            return;
        }

        ClearError();
        LastMessage = message;

        var sessionId = await EnsureCurrentSessionAsync();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var resolvedSessionId = sessionId!;
        AddUserMessageAndUpdateTitle(resolvedSessionId, message);
        _sessionStore.UpdateSession(
            resolvedSessionId,
            session => session.ReasoningEnabled = ReasoningEnabled);

        await StartStreamingMessageAsync(resolvedSessionId, message);
    }

    private void AddUserMessageAndUpdateTitle(string sessionId, string message)
    {
        _sessionStore.UpdateSession(sessionId, session =>
        {
            session.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = message
            });

            if (session.Messages.Count == 1)
            {
                session.Title = ChatProviderResolver.GenerateSessionTitle(message);
            }
        });
    }

    private async Task StartStreamingMessageAsync(string sessionId, string message)
    {
        IsSending = true;
        SetupCancellationToken();
        NotifyStateChanged();

        try
        {
            var session = _sessionStore.GetSession(sessionId);
            if (session == null)
            {
                SetPageError(_localizer["Error:Generic"]);
                return;
            }

            StreamingContent = _chatFacade.SendMessageStreamingAsync(
                session,
                message,
                CancellationToken);
            UpdateCurrentSession();
            NotifyStateChanged();
        }
        catch (OperationCanceledException)
        {
            SetStreamError(_localizer["Error:RequestCancelled"]);
        }
        catch (HttpRequestException ex)
        {
            SetStreamError($"{_localizer["Error:NetworkError"]}: {ex.Message}");
        }
        catch (Exception ex)
        {
            SetStreamError($"{_localizer["Error:Generic"]}: {ex.Message}");
        }
    }

    private void SetStreamError(string error)
    {
        IsSending = false;
        StreamingContent = null;
        SetError(error, canRetry: true);
        _snackbar.Add(error, Severity.Error);
        NotifyStateChanged();
    }

    private void CompleteStream(string content)
    {
        IsSending = false;
        StreamingContent = null;
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
        CancellationToken = CancellationToken.None;
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    private async Task CancelAsync()
    {
        if (CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested)
        {
            await CancellationTokenSource.CancelAsync();
            _snackbar.Add(_localizer["Chat:Status:GenerationStopped"], Severity.Info);
        }

        IsSending = false;
        StreamingContent = null;
        NotifyStateChanged();
    }

    private async Task RetryLastMessageAsync()
    {
        if (string.IsNullOrEmpty(LastMessage))
        {
            return;
        }

        ClearError();
        await SendMessageAsync(LastMessage);
    }

    private void EditMessage((AIChatMessage Message, string NewContent) args)
    {
        if (string.IsNullOrWhiteSpace(args.NewContent) || IsSending)
        {
            return;
        }

        ClearError();
        LastMessage = args.NewContent;
        IsSending = true;
        SetupCancellationToken();

        _sessionStore.UpdateSession(
            _sessionStore.CurrentSessionId!,
            session => session.ReasoningEnabled = ReasoningEnabled);
        var session = _sessionStore.CurrentSession;
        if (session == null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        StreamingContent = _chatFacade.EditMessageAsync(
            session,
            args.Message.Id,
            args.NewContent,
            CancellationToken);

        UpdateCurrentSession();
        NotifyStateChanged();
    }

    private void RetryMessage(AIChatMessage message)
    {
        if (IsSending)
        {
            return;
        }

        ClearError();
        IsSending = true;
        SetupCancellationToken();

        _sessionStore.UpdateSession(
            _sessionStore.CurrentSessionId!,
            session => session.ReasoningEnabled = ReasoningEnabled);
        var session = _sessionStore.CurrentSession;
        if (session == null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        StreamingContent = _chatFacade.RetryMessageAsync(
            session,
            message.Id,
            CancellationToken);

        UpdateCurrentSession();
        NotifyStateChanged();
    }

    private void DismissError()
    {
        ClearError();
        NotifyStateChanged();
    }
}
