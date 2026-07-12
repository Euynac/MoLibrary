using System.Net.Http;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Components;
using Monica.AI.UI.UIChat.Models;
using Monica.AI.UI.UIChat.Support;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task SendMessageAsync(ChatSendRequest request)
    {
        var message = request.Message;
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
        _sessionStore.UpdateSession(
            resolvedSessionId,
            session =>
            {
                _ = _chatFacade.UpdateSettings(
                    session,
                    session.Settings with { ReasoningEnabled = ReasoningEnabled });
                _ = _chatFacade.UpdateRuntimeContext(
                    session,
                    BuildRuntimeContext(SelectedKnowledgeBaseIds));
                if (session.Messages.Count == 0)
                {
                    _ = _chatFacade.Rename(session, ChatProviderResolver.GenerateSessionTitle(message));
                }
            });

        await StartStreamingMessageAsync(resolvedSessionId, message);
    }

    private async Task StartStreamingMessageAsync(string sessionId, string message)
    {
        IsSending = true;
        SetupCancellationToken();
        NotifyStateChanged();
        ChatSession? session = null;

        try
        {
            session = _sessionStore.GetSession(sessionId);
            if (session == null)
            {
                SetPageError(_localizer["Error:Generic"]);
                return;
            }

            StreamingState = new ChatStreamingState();
            UpdateCurrentSession();
            NotifyStateChanged();
            await ConsumeStreamAsync(
                session,
                _chatFacade.SendMessageStreamingAsync(session, message, CancellationToken));
        }
        catch (OperationCanceledException)
        {
            CompleteStream();
        }
        catch (HttpRequestException ex)
        {
            RecordStreamError(session, $"{_localizer["Error:NetworkError"]}: {ex.Message}");
        }
        catch (Exception ex)
        {
            RecordStreamError(session, $"{_localizer["Error:Generic"]}: {ex.Message}");
        }
    }

    private void RecordStreamError(ChatSession? session, string error)
    {
        if (session is null)
        {
            SetPageError(error);
            return;
        }

        _ = _chatFacade.RecordError(session, error);
        CompleteStream();
    }

    private void CompleteStream()
    {
        IsSending = false;
        StreamingState = null;
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

        NotifyStateChanged();
    }

    private async Task RetryLastMessageAsync()
    {
        if (string.IsNullOrEmpty(LastMessage))
        {
            return;
        }

        ClearError();
        await SendMessageAsync(new ChatSendRequest(LastMessage));
    }

    private async Task EditMessage((AIChatMessage Message, string NewContent) args)
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
            session => _ = _chatFacade.UpdateSettings(
                session,
                session.Settings with { ReasoningEnabled = ReasoningEnabled }));
        var session = _sessionStore.CurrentSession;
        if (session == null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        StreamingState = new ChatStreamingState();
        UpdateCurrentSession();
        NotifyStateChanged();
        await ConsumeStreamAsync(
            session,
            _chatFacade.EditMessageAsync(session, args.Message.Id, args.NewContent, CancellationToken));
    }

    private async Task RetryMessage(AIChatMessage message)
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
            session => _ = _chatFacade.UpdateSettings(
                session,
                session.Settings with { ReasoningEnabled = ReasoningEnabled }));
        var session = _sessionStore.CurrentSession;
        if (session == null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        StreamingState = new ChatStreamingState();
        UpdateCurrentSession();
        NotifyStateChanged();
        await ConsumeStreamAsync(
            session,
            _chatFacade.RetryMessageAsync(session, message.Id, CancellationToken));
    }

    private void DismissError()
    {
        ClearError();
        NotifyStateChanged();
    }

    private async Task ConsumeStreamAsync(
        ChatSession session,
        IAsyncEnumerable<Res<ChatStreamEvent>> stream)
    {
        ChatApprovalRequestEvent? pendingApproval = null;
        try
        {
            await foreach (var result in stream)
            {
                if (result.IsFailed(out var error, out var streamEvent))
                {
                    RecordStreamError(session, error.Message ?? _localizer["Error:Generic"]);
                    return;
                }

                StreamingState ??= new ChatStreamingState();
                StreamingState.Apply(streamEvent);
                pendingApproval = streamEvent as ChatApprovalRequestEvent ?? pendingApproval;
                UpdateCurrentSession();
                NotifyStateChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // The chat facade normally converts cancellation to a completion event. This guard also
            // covers cancellation before the first provider update is available.
        }
        catch (Exception ex)
        {
            RecordStreamError(session, $"{_localizer["Error:Generic"]}: {ex.Message}");
            return;
        }

        if (pendingApproval is not null)
        {
            await ContinueAfterApprovalAsync(session, pendingApproval);
            return;
        }

        CompleteStream();
    }

    private async Task ContinueAfterApprovalAsync(
        ChatSession session,
        ChatApprovalRequestEvent approval)
    {
        var parameters = new DialogParameters
        {
            [nameof(ExternalScriptApprovalDialog.Request)] = approval
        };
        var dialog = await _dialogService.ShowAsync<ExternalScriptApprovalDialog>(
            _localizer["Chat:Approval:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = false });
        var result = await dialog.Result;
        var approved = result is { Canceled: false, Data: true };
        StreamingState?.ResumeAfterApproval();
        IsSending = true;
        NotifyStateChanged();

        var reason = approved
            ? _localizer["Chat:Approval:ApprovedReason"].Value
            : _localizer["Chat:Approval:RejectedReason"].Value;
        await ConsumeStreamAsync(
            session,
            _chatFacade.ContinueApprovalAsync(
                session,
                approval.ApprovalId,
                approved,
                reason,
                CancellationToken));
    }
}
